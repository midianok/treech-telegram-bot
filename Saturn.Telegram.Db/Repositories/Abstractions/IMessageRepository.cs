using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Repositories.Abstractions;

public interface IMessageRepository
{
    Task<List<MessageEntity>> GetMessageChainAsync(long chatId, long messageId);
}