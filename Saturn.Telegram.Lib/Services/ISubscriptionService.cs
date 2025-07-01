using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Lib.Services;

public interface ISubscriptionService
{
    Task AddSubscriptionAsync(long userId, DateTime validUntil, SubscriptionType type);
    
    Task<bool> HasSubscriptionAsync(long userId, SubscriptionType type);
}