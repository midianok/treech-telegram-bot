namespace Saturn.Telegram.Db.CacheInvalidation;

public interface ICacheInvalidator
{
    Task InvalidateAgentAsync(Guid agentId, CancellationToken cancellationToken = default);
    Task InvalidateChatAsync(long chatId, CancellationToken cancellationToken = default);
    Task InvalidateImagePromptsAsync(CancellationToken cancellationToken = default);
}
