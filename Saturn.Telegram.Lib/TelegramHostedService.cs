using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Lib;

internal class TelegramHostedService : IHostedService
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly IEnumerable<OperationBase> _operations;
    private readonly ILogger<TelegramHostedService> _hostedServiceLogger;
    private readonly ILogger<OperationBase> _operationBaseLogger;
    

    public TelegramHostedService(
        TelegramBotClient telegramBotClient,
        IEnumerable<OperationBase> operations,
        ILogger<TelegramHostedService> hostedServiceLogger,
        ILogger<OperationBase> operationBaseLogger)
    {
        _operations = operations;
        _telegramBotClient = telegramBotClient;
        _hostedServiceLogger = hostedServiceLogger;
        _operationBaseLogger = operationBaseLogger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _telegramBotClient.DropPendingUpdates(cancellationToken: cancellationToken);
        var operations = _operations.Select(x => x.GetType().Name);
        _hostedServiceLogger.LogInformation("Starting hosted service: {operations}", string.Join(", ", operations));
        
        foreach (var operation in _operations)
        {
            operation
                .SetService("TelegramBotClient", _telegramBotClient)
                .SetService("Logger", _operationBaseLogger);

            _telegramBotClient.OnError += operation.OnErrorAsync;
            _telegramBotClient.OnMessage += operation.OnMessageAsync;
            _telegramBotClient.OnUpdate += operation.OnUpdateAsync;
        }
    }
    
    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}