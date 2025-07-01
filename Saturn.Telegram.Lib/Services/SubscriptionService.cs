using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Lib.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly IDbContextFactory<SaturnContext> _contextFactory;
    private readonly ILogger<SubscriptionService> _logger;
    private readonly IMemoryCache _memoryCache;

    public SubscriptionService(IDbContextFactory<SaturnContext> contextFactory, ILogger<SubscriptionService> logger, IMemoryCache memoryCache)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _memoryCache = memoryCache;
    }

    public async Task AddSubscriptionAsync(long userId, DateTime validUntil, SubscriptionType type)
    {
        var cacheKey = $"SubscriptionEntity_{userId}_{type}";
        var context = await _contextFactory.CreateDbContextAsync();
        await context.Subscriptions.AddAsync(new SubscriptionEntity
        {
            UserId = userId,
            ValidUntil = validUntil,
            Type = type,
            Date = DateTime.Now
        });
        await context.SaveChangesAsync();
        if (_memoryCache.TryGetValue(cacheKey, out bool value))
        {
            _memoryCache.Remove(cacheKey);
        }
    }

    public async Task<bool> HasSubscriptionAsync(long userId, SubscriptionType type)
    {
        var cacheKey = $"SubscriptionEntity_{userId}_{type}";
        if (_memoryCache.TryGetValue(cacheKey, out bool value))
        {
            return value;
        }
        
        var context = await _contextFactory.CreateDbContextAsync();
        var subscription = await context.Subscriptions.FirstOrDefaultAsync(x => x.UserId == userId && x.Type == type && x.ValidUntil > DateTime.Now);
        var hasSubscription = subscription != null;
        
        _memoryCache.Set(cacheKey, hasSubscription, TimeSpan.FromHours(10));
        
        return hasSubscription;
    }
}