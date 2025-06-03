using System.Globalization;
using Humanizer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Saturn.Telegram.Lib.Attributes;
using Saturn.Telegram.Lib.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
// ReSharper disable UnassignedReadonlyField

namespace Saturn.Telegram.Lib.Operation;

public abstract class OperationBase : IOperation
{
    protected virtual bool CooldownNeeded => false;
    
    protected readonly ILogger<OperationBase> Logger;
    protected readonly TelegramBotClient TelegramBotClient;
    protected readonly IMemoryCache MemoryCache;
    protected readonly ICooldownService CooldownService;

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        var isMatch = ValidateOnTextMessage(msg, type) && ValidateOnMessageFluent(msg, type);
        if (!isMatch)
        {
            return;
        }

        if (CooldownNeeded)
        {
            var (inCooldown, message) = await CooldownService.IfInCooldown(GetType().Name, msg.Chat.Id, msg.From!.Id);
            if (inCooldown)
            {
                await TelegramBotClient.SendMessage(msg.Chat.Id, message!, replyParameters: new ReplyParameters { MessageId = msg.MessageId });
                return;
            }
        }

        await ProcessOnMessageAsync(msg, type);

        if (CooldownNeeded)
        {
           await CooldownService.SetCooldown(GetType().Name, msg.Chat.Id, msg.From!.Id);
        }
    }

    public Task OnUpdateAsync(Update update)
    {
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception exception, HandleErrorSource source)
    {
        Logger.LogError(exception, "Ошибка");
        return Task.CompletedTask;
    }

    private bool ValidateOnMessageFluent(Message msg, UpdateType type)
    {
        var validator = new MessageValidator();
        FluentValidateOnMessage(validator);
        var result = validator.Validate((msg,type));
        return result.IsValid;
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

    protected virtual void FluentValidateOnMessage(MessageValidator validator) { }
}