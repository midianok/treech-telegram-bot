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

    /// <summary>Stickers people (not bots) send most often in the chat, most popular first.</summary>
    Task<List<PopularSticker>> GetPopularStickersAsync(long chatId, int limit, CancellationToken cancellationToken);

    /// <summary>Fills in the emoji for saved messages with this sticker that don't have it yet.</summary>
    Task SetStickerEmojiAsync(string stickerId, string emoji, CancellationToken cancellationToken);
}

public record ChatAuthor(long UserId, string? Username, string? FirstName, string? LastName, int MessageCount);

public record PopularSticker(string StickerId, string? Emoji, int UseCount);