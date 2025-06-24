using System.Globalization;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Lib.Services;

public class CooldownService : ICooldownService
{
    private readonly IDbContextFactory<SaturnContext> _contextFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<CooldownService> _logger;
    private readonly CooldownEntity _defaultCooldown;

    public CooldownService(IDbContextFactory<SaturnContext> contextFactory, IMemoryCache memoryCache, ILogger<CooldownService> logger)
    {
        _contextFactory = contextFactory;
        _memoryCache = memoryCache;
        _logger = logger;
        _defaultCooldown = new CooldownEntity
        {
            CooldownSeconds = 60,
            Message = "Команду можно выполнить через {cooldown}"
        };
    }

    public async Task<(bool cooldown, string? CooldownMessage)> IfInCooldown(string operationType, long chatId, long userId)
    {
        var cooldown = await GetCooldown(operationType, chatId, userId);
        if (cooldown.CooldownSeconds == 0)
        {
            return (false, null);
        }
        
        var cacheKey = $"cooldown_{operationType}_{chatId}_{userId}";

        if (!_memoryCache.TryGetValue(cacheKey, out DateTime cooldownTime))
        {
            return (false, null);
        }
        
        
        var elapsed = (cooldownTime - DateTime.Now).Humanize(2, culture: new CultureInfo("ru-RU"), collectionSeparator: " ");
        var message = !string.IsNullOrEmpty(cooldown.Message) ? cooldown.Message.Replace("{cooldown}", elapsed) : string.Empty;

        return (true, message);
    }

    public async Task SetCooldown(string operationType, long chatId, long userId)
    {
        var cooldown = await GetCooldown(operationType, chatId, userId);

        if (cooldown.CooldownSeconds == 0)
        {
            return;
        }

        var cacheKey = $"cooldown_{operationType}_{chatId}_{userId}";
        
        _memoryCache.Set(cacheKey, DateTime.Now.AddSeconds(cooldown.CooldownSeconds), TimeSpan.FromSeconds(cooldown.CooldownSeconds));    
    }

    private async Task<CooldownEntity> GetCooldown(string operationType, long chatId, long userId)
    {
        var cacheKey = $"CooldownEntity_{operationType}_{chatId}_{userId}";

        if (_memoryCache.TryGetValue(cacheKey, out CooldownEntity? cachedCooldown))
        {
            return cachedCooldown ?? _defaultCooldown;
        }

        var context = await _contextFactory.CreateDbContextAsync();
        var cooldown = await context.Cooldowns
                           .Where(x => x.Operation == operationType && x.ChatId == chatId && (x.UserId == userId || x.UserId == null))
                           .OrderByDescending(x => x.UserId == userId)
                           .FirstOrDefaultAsync()
                       ?? _defaultCooldown;
        _logger.LogInformation("Cooldown for {OperationType} in chat {ChatId} for user {UserId} is {CooldownCooldownSeconds}", operationType, chatId, userId, cooldown.CooldownSeconds);
        
        _memoryCache.Set(cacheKey, cooldown, TimeSpan.FromMinutes(10));

        return cooldown;
    }
}