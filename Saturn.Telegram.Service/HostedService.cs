using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Saturn.Bot.Service.Database;
using Saturn.Bot.Service.Operations.Abstractions;
using Telegram.Bot;

namespace Saturn.Bot.Service;

public class HostedService : IHostedService
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly IEnumerable<IOperation> _operations;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;
    private readonly IConfiguration _configuration;

    public HostedService(IEnumerable<IOperation> operations, IConfiguration configuration, IDbContextFactory<SaturnContext> contextFactory)
    {
        _contextFactory = contextFactory;
        _cancellationTokenSource = new CancellationTokenSource();
        _operations = operations;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var botToken = _configuration.GetSection("BOT_TOKEN").Value;
        if (string.IsNullOrWhiteSpace(botToken))
        {
            throw new Exception("Env variable BOT_TOKEN not presented");
        }
        var bot = new TelegramBotClient(botToken, cancellationToken: _cancellationTokenSource.Token);

        var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        foreach (var operation in _operations)
        {
            bot.OnError += operation.OnErrorAsync;
            bot.OnMessage += operation.OnMessageAsync;
            bot.OnUpdate += operation.OnUpdateAsync;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _cancellationTokenSource.CancelAsync();
    }
}