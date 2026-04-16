using Microsoft.Extensions.Caching.Memory;
using Saturn.Telegram.Lib.Attributes;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Saturn.Telegram.Lib.Infrastructure;

public class CooldownService : ICooldownService
{
    private const string DefaultCooldownMessage = "Слишком часто. Следующий раз можно через {elapsed}.";
    private const string DefaultGlobalCooldownMessage = "Лимит вызовов на этот час исчерпан.";

    private readonly IMemoryCache _cache;
    private readonly TelegramBotClient _botClient;

    public CooldownService(IMemoryCache cache, TelegramBotClient botClient)
    {
        _cache = cache;
        _botClient = botClient;
    }

    public async Task<bool> IsCooldownAsync(IOperation operation, Message msg)
    {
        if (msg.From?.Username == "ilya_naprimer")
        {
            return false;
        }

        if (await IsGlobalCooldownAsync(operation, msg))
        {
            return true;
        }

        var cooldown = GetCooldown(operation);
        if (cooldown == null || msg.From == null)
        {
            return false;
        }

        var cacheKey = BuildCacheKey(operation, msg.From.Id);
        if (!_cache.TryGetValue(cacheKey, out DateTimeOffset readyAt))
        {
            return false;
        }

        var remaining = readyAt - DateTimeOffset.UtcNow;
        var text = (cooldown.Message ?? DefaultCooldownMessage)
            .Replace("{elapsed}", FormatDuration(remaining));

        await _botClient.SendMessage(msg.Chat, text,
            replyParameters: new ReplyParameters { MessageId = msg.Id });
        return true;
    }

    public void SetCooldown(IOperation operation, Message msg)
    {
        if (msg.From?.Username == "ilya_naprimer")
        {
            return;
        }

        IncrementGlobalCooldown(operation);

        var cooldown = GetCooldown(operation);
        if (cooldown == null || msg.From == null)
        {
            return;
        }

        var cacheKey = BuildCacheKey(operation, msg.From.Id);
        var readyAt = DateTimeOffset.UtcNow.AddSeconds(cooldown.Seconds);
        _cache.Set(cacheKey, readyAt, TimeSpan.FromSeconds(cooldown.Seconds));
    }

    private async Task<bool> IsGlobalCooldownAsync(IOperation operation, Message msg)
    {
        var globalCooldown = GetGlobalCooldown(operation);
        if (globalCooldown == null)
        {
            return false;
        }

        var counter = GetOrCreateGlobalCounter(operation);
        if (counter.Count < globalCooldown.MaxCallsPerHour)
        {
            return false;
        }

        var text = globalCooldown.Message ?? DefaultGlobalCooldownMessage;
        await _botClient.SendMessage(msg.Chat, text,
            replyParameters: new ReplyParameters { MessageId = msg.Id });
        return true;
    }

    private void IncrementGlobalCooldown(IOperation operation)
    {
        var globalCooldown = GetGlobalCooldown(operation);
        if (globalCooldown == null)
        {
            return;
        }

        var counter = GetOrCreateGlobalCounter(operation);
        Interlocked.Increment(ref counter.Count);
    }

    private GlobalCounter GetOrCreateGlobalCounter(IOperation operation)
    {
        var now = DateTimeOffset.UtcNow;
        var nextHour = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero).AddHours(1);
        var cacheKey = BuildGlobalCacheKey(operation, now);
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpiration = nextHour;
            return new GlobalCounter();
        })!;
    }

    private static GlobalCooldownAttribute? GetGlobalCooldown(IOperation operation) =>
        operation.GetType().GetCustomAttributes(typeof(GlobalCooldownAttribute), false)
            .FirstOrDefault() as GlobalCooldownAttribute;

    private static CooldownAttribute? GetCooldown(IOperation operation) =>
        operation.GetType().GetCustomAttributes(typeof(CooldownAttribute), false)
            .FirstOrDefault() as CooldownAttribute;

    private static string BuildCacheKey(IOperation operation, long userId) =>
        $"cooldown:{operation.GetType().FullName}:{userId}";

    private static string BuildGlobalCacheKey(IOperation operation, DateTimeOffset now) =>
        $"global_cooldown:{operation.GetType().FullName}:{now:yyyyMMddHH}";

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds < 1)
        {
            return "меньше секунды";
        }

        var minutes = (int)duration.TotalMinutes;
        var seconds = duration.Seconds;

        if (minutes > 0)
        {
            return $"{minutes} мин {seconds} сек";
        }

        return $"{seconds} сек";
    }

    private sealed class GlobalCounter
    {
        public int Count;
    }
}
