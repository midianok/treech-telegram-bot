using System.Reflection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;

namespace Saturn.Telegram.Lib;

public class HostedService : IHostedService
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly IEnumerable<IOperation> _operations;
    private readonly ILogger<OperationBase> _logger;
    private readonly IMemoryCache _memoryCache;

    public HostedService(
        TelegramBotClient telegramBotClient,
        IEnumerable<IOperation> operations,
        ILogger<OperationBase> logger,
        IMemoryCache memoryCache)
    {
        _operations = operations;
        _logger = logger;
        _memoryCache = memoryCache;
        _telegramBotClient = telegramBotClient;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var operation in _operations)
        {
            var type = operation.GetType();
            type.GetField("TelegramBotClient", BindingFlags.Instance | BindingFlags.NonPublic)?
                .SetValue(operation, _telegramBotClient);
            
            type.GetField("Logger", BindingFlags.Instance | BindingFlags.NonPublic)?
                .SetValue(operation, _logger);
            
            type.GetField("MemoryCache", BindingFlags.Instance | BindingFlags.NonPublic)?
                .SetValue(operation, _memoryCache);
            
            _telegramBotClient.OnError += operation.OnErrorAsync;
            _telegramBotClient.OnMessage += operation.OnMessageAsync;
            _telegramBotClient.OnUpdate += operation.OnUpdateAsync;
        }
        
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}