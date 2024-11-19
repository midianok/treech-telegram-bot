using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Saturn.Bot.Service.Database;
using Saturn.Bot.Service.Extension;
using Saturn.Bot.Service.Operations.Abstractions;
using Telegram.Bot;

namespace Saturn.Bot.Service;

public class HostedService : IHostedService
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly IEnumerable<IOperation> _operations;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    public HostedService(TelegramBotClient telegramBotClient, IEnumerable<IOperation> operations, IDbContextFactory<SaturnContext> contextFactory)
    {
        _contextFactory = contextFactory;
        _operations = operations;
        _telegramBotClient = telegramBotClient;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        foreach (var operation in _operations)
        {
            _telegramBotClient.OnError += operation.OnErrorAsync;
            _telegramBotClient.OnMessage += operation.OnMessageAsync;
            _telegramBotClient.OnUpdate += operation.OnUpdateAsync;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}