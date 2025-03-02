using Microsoft.Extensions.Hosting;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;

namespace Saturn.Telegram.Lib;

public class HostedService : IHostedService
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly IEnumerable<IOperation> _operations;

    public HostedService(TelegramBotClient telegramBotClient, IEnumerable<IOperation> operations)
    {
        _operations = operations;
        _telegramBotClient = telegramBotClient;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var operation in _operations.Where(x => x.Enabled()))
        {
            _telegramBotClient.OnError += operation.OnErrorAsync;
            _telegramBotClient.OnMessage += operation.OnMessageAsync;
            _telegramBotClient.OnUpdate += operation.OnUpdateAsync;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}