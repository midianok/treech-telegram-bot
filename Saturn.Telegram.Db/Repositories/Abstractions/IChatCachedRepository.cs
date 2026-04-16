using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Repositories.Abstractions;

public interface IChatCachedRepository
{
    Task<ChatEntity> GetAsync(long chatId);
    Task SetAiAgentAsync(long chatId, Guid agentId, CancellationToken cancellationToken = default);
    Task InvalidateByAgentAsync(Guid agentId, CancellationToken cancellationToken = default);
    Task InvalidateChatAsync(long chatId);
}