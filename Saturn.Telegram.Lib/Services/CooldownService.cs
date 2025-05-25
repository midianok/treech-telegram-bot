using System.Globalization;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Lib.Services;

public class CooldownService : ICooldownService
{
    private readonly IDbContextFactory<SaturnContext> _contextFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly CooldownEntity _defaultCooldown;

    public CooldownService(IDbContextFactory<SaturnContext> contextFactory, IMemoryCache memoryCache)
    {
        _contextFactory = contextFactory;
        _memoryCache = memoryCache;
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
        
        var cacheKey = $"{operationType}_{chatId}_{userId}";

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

        var cacheKey = $"{operationType}_{chatId}_{userId}";
        
        _memoryCache.Set(cacheKey, DateTime.Now.AddSeconds(cooldown.CooldownSeconds), TimeSpan.FromSeconds(cooldown.CooldownSeconds));    
    }

    private async Task<CooldownEntity> GetCooldown(string operationType, long chatId, long userId)
    {
        var context = await _contextFactory.CreateDbContextAsync();
        
        var cooldown = await context.Cooldowns.SingleOrDefaultAsync(x => x.Operation == operationType && x.ChatId == chatId && x.UserId == userId) ?? 
                       await context.Cooldowns.SingleOrDefaultAsync(x => x.Operation == operationType && x.ChatId == chatId) ?? 
                       _defaultCooldown;
        return cooldown;
    }
}