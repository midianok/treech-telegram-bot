using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Saturn.Telegram.Lib.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Lib;

internal sealed class TelegramHostedService : IHostedService
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly ILogger<TelegramHostedService> _logger;
    private readonly IOperationManager _operationManager;

    private readonly CancellationTokenSource _stoppingCts = new();
    private readonly ConcurrentDictionary<Task, byte> _inFlight = new();

    public TelegramHostedService(
        TelegramBotClient telegramBotClient,
        ILogger<TelegramHostedService> logger,
        IOperationManager operationManager)
    {
        _telegramBotClient = telegramBotClient;
        _logger = logger;
        _operationManager = operationManager;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _telegramBotClient.DropPendingUpdates(cancellationToken: cancellationToken);

        _telegramBotClient.OnMessage += OnMessage;
        _telegramBotClient.OnUpdate += OnUpdate;
        _telegramBotClient.OnError += _operationManager.ErrorHandler;

        _logger.LogInformation("Telegram bot started");
    }

    private Task OnMessage(Message msg, UpdateType type)
    {
        Track(() => _operationManager.MessageHandler(msg, type, _stoppingCts.Token));
        return Task.CompletedTask;
    }

    private Task OnUpdate(Update update)
    {
        Track(() => _operationManager.UpdateHandler(update));
        return Task.CompletedTask;
    }

    private void Track(Func<Task> handler)
    {
        if (_stoppingCts.IsCancellationRequested)
        {
            return;
        }

        var task = Task.Run(handler, _stoppingCts.Token);
        _inFlight.TryAdd(task, 0);
        
        _ = task.ContinueWith(
            completed =>
            {
                _inFlight.TryRemove(completed, out _);
                if (completed.IsFaulted)
                {
                    _logger.LogError(completed.Exception, "Unhandled exception in Telegram update handler");
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _telegramBotClient.OnMessage -= OnMessage;
        _telegramBotClient.OnUpdate -= OnUpdate;
        _telegramBotClient.OnError -= _operationManager.ErrorHandler;

        await _stoppingCts.CancelAsync();

        var pending = _inFlight.Keys.ToArray();
        if (pending.Length > 0)
        {
            _logger.LogInformation("Draining {Count} in-flight handlers", pending.Length);
            
            var drain = Task.WhenAll(pending);
            var finished = await Task.WhenAny(drain, Task.Delay(Timeout.Infinite, cancellationToken));
            if (finished != drain)
            {
                _logger.LogWarning("Shutdown timeout reached, {Count} handlers still running", _inFlight.Count);
            }
        }

        _stoppingCts.Dispose();
        _logger.LogInformation("Telegram bot stopped");
    }
}
