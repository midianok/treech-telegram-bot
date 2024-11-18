using Microsoft.Extensions.Logging;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Abstractions;

public abstract class OperationBase : IOperation
{
    private readonly ILogger _logger;

    protected OperationBase(ILogger<IOperation> logger)
    {
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