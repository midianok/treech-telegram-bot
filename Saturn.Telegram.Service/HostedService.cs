using Microsoft.Extensions.Hosting;
using Saturn.Bot.Service.Operations;
using Saturn.Bot.Service.Operations.Abstractions;
using Telegram.Bot;

namespace Saturn.Bot.Service;

public class HostedService : IHostedService
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly IEnumerable<IOperation> _operations;

    public HostedService(IEnumerable<IOperation> operations, TelegramBotClient telegramBotClient)
    {
        _telegramBotClient = telegramBotClient;
        _cancellationTokenSource = new CancellationTokenSource();
        _operations = operations;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var operation in _operations)
        {
            _telegramBotClient.OnError += operation.OnErrorAsync;
            _telegramBotClient.OnMessage += operation.OnMessageAsync;
            _telegramBotClient.OnUpdate += operation.OnUpdateAsync;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _cancellationTokenSource.CancelAsync();
    }
}