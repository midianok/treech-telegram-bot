using Microsoft.Extensions.Logging;
using Saturn.Telegram.Lib.Services.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace Saturn.Telegram.Lib.Operation;

public abstract class OperationBase : IOperation
{
    protected readonly ILogger<OperationBase> Logger;
    protected readonly TelegramBotClient TelegramBotClient;
    protected readonly ISubscriptionService SubscriptionService;

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        var isMatch = ValidateTextMessage(msg, type);
        if (!isMatch)
        {
            return;
        }

        await ProcessOnMessageAsync(msg, type);
        
    }

    public Task OnUpdateAsync(Update update)
    {
        return ProcessOnUpdateAsync(update);
    }

    public Task OnErrorAsync(Exception exception, HandleErrorSource source)
    {
        Logger.LogError(exception, "Ошибка");
        return Task.CompletedTask;
    }

    private bool ValidateTextMessage(Message msg, UpdateType type)
    {
        if (UpdateType.Message != type || string.IsNullOrEmpty(msg.Text))
        {
            return false;
        }
        
        return ValidateOnTextMessage(msg, type);
    }
    
    protected virtual bool ValidateOnTextMessage(Message msg, UpdateType type) => true;

    protected virtual Task ProcessOnMessageAsync(Message msg, UpdateType type) => Task.CompletedTask;
    
    protected virtual Task ProcessOnUpdateAsync(Update update) => Task.CompletedTask;
}