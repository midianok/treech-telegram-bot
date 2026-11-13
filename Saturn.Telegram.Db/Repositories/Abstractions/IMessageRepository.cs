using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Repositories.Abstractions;

public interface IMessageRepository
{
    Task<List<MessageEntity>> GetMessageChainAsync(long chatId, long messageId);

    Task<List<MessageEntity>> SearchMessagesAsync(
        long chatId,
        long? userId,
        DateTime? from,
        DateTime? to,
        string? text,
        int limit,
        CancellationToken cancellationToken);

    Task<List<ChatAuthor>> GetChatAuthorsAsync(long chatId, CancellationToken cancellationToken);
}

public record ChatAuthor(long UserId, string? Username, string? FirstName, string? LastName, int MessageCount);