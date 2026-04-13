using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace Saturn.Telegram.Lib;

internal class TelegramHostedService : IHostedService
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly ILogger<TelegramHostedService> _logger;
    private readonly OperationManager _operationManager;

    public TelegramHostedService(
        TelegramBotClient telegramBotClient,
        ILogger<TelegramHostedService> logger,
        OperationManager operationManager)
    {
        _telegramBotClient = telegramBotClient;
        _logger = logger;
        _operationManager = operationManager;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _telegramBotClient.DropPendingUpdates(cancellationToken: cancellationToken);
        
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
        
        _logger.LogInformation("Telegram bot started");
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
