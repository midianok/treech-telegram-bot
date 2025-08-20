using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;

namespace Saturn.Telegram.Lib;

internal class TelegramHostedService : IHostedService
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly IEnumerable<OperationBase> _operations;
    private readonly ILogger<OperationBase> _logger;

    public TelegramHostedService(
        TelegramBotClient telegramBotClient,
        IEnumerable<OperationBase> operations,
        ILogger<OperationBase> logger)
    {
        _operations = operations;
        _logger = logger;
        _telegramBotClient = telegramBotClient;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _telegramBotClient.DropPendingUpdates(cancellationToken: cancellationToken);
        var operations = _operations.Select(x => x.GetType().Name);
        _logger.LogInformation("Starting hosted service: {operations}", string.Join(", ", operations));
        
        foreach (var operation in _operations)
        {
            operation
                .SetService("TelegramBotClient", _telegramBotClient)
                .SetService("Logger", _logger);

            _telegramBotClient.Use(operation);
        }
    }
    
    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}