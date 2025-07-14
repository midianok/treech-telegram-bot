using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Lib.Services;
using Saturn.Telegram.Lib.Services.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace Saturn.Telegram.Lib.Operation;

public abstract class OperationBase : IOperation
{
    protected virtual bool CooldownNeeded => false;
    protected virtual SubscriptionType SubscriptionType => SubscriptionType.None;
    protected readonly ILogger<OperationBase> Logger;
    protected readonly TelegramBotClient TelegramBotClient;
    protected readonly ICooldownService CooldownService;
    protected readonly ISubscriptionService SubscriptionService;

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        var isMatch = ValidateTextMessage(msg, type);
        if (!isMatch)
        {
            return;
        }

        var hasSubscription = await SubscriptionService.HasSubscriptionAsync(msg.From!.Id, SubscriptionType);
        if (CooldownNeeded && !hasSubscription)
        {
            var (inCooldown, message) = await CooldownService.IfInCooldown(GetType().Name, msg.Chat.Id, msg.From!.Id);
            
            if (inCooldown)
            {
                await OnCooldownAsync( msg, type, message!);
                return;
            }
        }

        await ProcessOnMessageAsync(msg, type);

        if (CooldownNeeded && !hasSubscription)
        {
           await CooldownService.SetCooldown(GetType().Name, msg.Chat.Id, msg.From!.Id);
        }
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

    protected virtual Task OnCooldownAsync(Message msg, UpdateType type, string cooldownMessage) => 
        TelegramBotClient.SendMessage(msg.Chat.Id, cooldownMessage, ParseMode.MarkdownV2, new ReplyParameters { MessageId = msg.MessageId }, linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true });
}