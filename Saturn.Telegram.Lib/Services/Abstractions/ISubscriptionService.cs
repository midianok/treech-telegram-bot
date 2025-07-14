using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Lib.Services.Abstractions;

public interface ISubscriptionService
{
    Task AddSubscriptionAsync(long userId, DateTime validUntil, SubscriptionType type);
    
    Task<bool> HasSubscriptionAsync(long userId, SubscriptionType type);
}