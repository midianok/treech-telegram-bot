using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Operation;
using Saturn.Telegram.Lib.Services.Abstractions;
using Telegram.Bot;

namespace Saturn.Telegram.Lib;

public class HostedService : IHostedService
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly IEnumerable<IOperation> _operations;
    private readonly ILogger<OperationBase> _logger;
    private readonly ISaveMessageService _saveMessageService;

    public HostedService(
        TelegramBotClient telegramBotClient,
        IEnumerable<IOperation> operations,
        ILogger<OperationBase> logger,
        ISaveMessageService saveMessageService)
    {
        _operations = operations;
        _logger = logger;
        _saveMessageService = saveMessageService;
        _telegramBotClient = telegramBotClient;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _telegramBotClient.DropPendingUpdates(cancellationToken: cancellationToken);
        var operations = _operations.Select(x => x.GetType().Name);
        _logger.LogInformation("Starting hosted service: {operations}", string.Join(", ", operations));
        
        RegisterTelegramBotEventHandlers(new SaveMessageOperation(_saveMessageService));
        
        foreach (var operation in _operations)
        {
            operation
                .SetService("TelegramBotClient", _telegramBotClient)
                .SetService("Logger", _logger);

            RegisterTelegramBotEventHandlers(operation);
        }
    }

    private void RegisterTelegramBotEventHandlers(IOperation operation)
    {
        _telegramBotClient.OnError += operation.OnErrorAsync;
        _telegramBotClient.OnMessage += operation.OnMessageAsync;
        _telegramBotClient.OnUpdate += operation.OnUpdateAsync;
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}