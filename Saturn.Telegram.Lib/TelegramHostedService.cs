using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;

namespace Saturn.Telegram.Lib;

internal class TelegramHostedService : IHostedService
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly IEnumerable<IOperation> _operations;
    private readonly ILogger<TelegramHostedService> _hostedServiceLogger;
    private readonly OperationManager _operationManager;

    public TelegramHostedService(
        TelegramBotClient telegramBotClient,
        IEnumerable<IOperation> operations,
        ILogger<TelegramHostedService> hostedServiceLogger,
        OperationManager operationManager)
    {
        _telegramBotClient = telegramBotClient;
        _operations = operations;
        _hostedServiceLogger = hostedServiceLogger;
        _operationManager = operationManager;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _telegramBotClient.DropPendingUpdates(cancellationToken: cancellationToken);
        var operationNames = _operations.Select(x => x.GetType().Name);
        _hostedServiceLogger.LogInformation("Starting hosted service. Enabled operations: {operations}", string.Join(", ", operationNames));
        
        _telegramBotClient.OnMessage += (msg, type) =>
        {
            _ = Task.Run(() => _operationManager.MessageHandler(msg, type), cancellationToken); 
            return Task.CompletedTask;
        };
        _telegramBotClient.OnUpdate += msg =>
        {
            _ = Task.Run(() => _operationManager.UpdateHandler(msg), cancellationToken); 
            return Task.CompletedTask;
        };
        _telegramBotClient.OnError += _operationManager.ErrorHandler;
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
