using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Saturn.Bot.Service.Options;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Abstractions;

public abstract class OperationBase : IOperation
{
    private readonly ILogger _logger;
    private readonly IConfiguration _configuration;

    public bool Enabled()
    {
        var enabledConfig = _configuration.GetSection($"{GetType().Name}Enabled").Value;
        bool.TryParse(enabledConfig, out var result);
        return result;
    }

    protected OperationBase(ILogger<IOperation> logger, IConfiguration configuration)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task OnMessageAsync(Message msg, UpdateType type)
    {
        var isMatch = ValidateOnMessage(msg, type);
        if (!isMatch)
        {
            return Task.CompletedTask;
        }

        return ProcessOnMessageAsync(msg, type);
    }

    public Task OnUpdateAsync(Update update)
    {
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception exception, HandleErrorSource source)
    {
        _logger.LogError(exception, "Ошибка");
        return Task.CompletedTask;
    }

    protected virtual bool ValidateOnMessage(Message msg, UpdateType type) => true;

    protected virtual Task ProcessOnMessageAsync(Message msg, UpdateType type) => Task.CompletedTask;
}