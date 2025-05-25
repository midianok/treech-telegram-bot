using System.Globalization;
using System.Reflection;
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
    protected readonly ILogger<OperationBase> Logger;
    protected readonly TelegramBotClient TelegramBotClient;
    protected readonly IMemoryCache MemoryCache;
    protected readonly ICooldownService CooldownService;

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        var isMatch = ValidateOnMessage(msg, type) && ValidateOnMessageFluent(msg, type);
        if (!isMatch || await InCooldown(msg))
        {
            return;
        }

        await ProcessOnMessageAsync(msg, type);
    }

    private async Task<bool> InCooldown(Message msg)
    {
        if (msg.From == null)
        {
            return false;
        }

        var user = await TelegramBotClient.GetChatMember(msg.Chat.Id, msg.From.Id);
        var cooldowns = GetType()
            .GetMethod(nameof(ProcessOnMessageAsync), BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetCustomAttributes<CooldownAttribute>()
            .Where(x => x.ChatId == msg.Chat.Id)
            .ToList();

        if (cooldowns.Count == 0)
        {
            return false;
        }
        
        var userNameCooldown = cooldowns.SingleOrDefault(x => x.UserName == msg.From.Username);
        if (userNameCooldown?.Cooldown > 0)
        {
            return await CheckCooldown(msg, userNameCooldown);
        }
        
        var statusCooldown = cooldowns.SingleOrDefault(x => x.UserStatus == user.Status);
        if (statusCooldown?.Cooldown > 0)
        {
            return await CheckCooldown(msg, statusCooldown);
        }

        return false;
    }

    private async Task<bool> CheckCooldown(Message msg, CooldownAttribute cooldown)
    {
        var cacheKey = $"{msg.Chat.Id}_{msg.From!.Id}";

        if (MemoryCache.TryGetValue(cacheKey, out DateTime cooldownTime))
        {
            var elapsed = (cooldownTime - DateTime.Now).Humanize(2, culture: new CultureInfo("ru-RU"), collectionSeparator: " ");
            var message = string.IsNullOrEmpty(cooldown.Message)
                ? $"Команду можно будет выполнить через {elapsed}"
                : cooldown.Message.Replace("{cooldown}", elapsed);

            await TelegramBotClient.SendMessage(msg.Chat.Id, message, replyParameters: new ReplyParameters { MessageId = msg.MessageId });
            return true;
        }

        MemoryCache.Set(cacheKey, DateTime.Now.AddSeconds(cooldown.Cooldown), TimeSpan.FromSeconds(cooldown.Cooldown));
        return false;
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
    
    
    protected virtual bool ValidateOnMessage(Message msg, UpdateType type) => true;

    protected virtual Task ProcessOnMessageAsync(Message msg, UpdateType type) => Task.CompletedTask;

    protected virtual void FluentValidateOnMessage(MessageValidator validator) { }
}