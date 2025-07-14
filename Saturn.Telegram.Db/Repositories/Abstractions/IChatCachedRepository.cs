using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Repositories.Abstractions;

public interface IChatCachedRepository
{
    Task<ChatEntity> GetAsync(long chatId);
}