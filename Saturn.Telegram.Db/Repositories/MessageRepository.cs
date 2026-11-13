using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Db.Repositories.Abstractions;

namespace Saturn.Telegram.Db.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly IDbContextFactory<SaturnContext> _dbContextFactory;
    private readonly ILogger<MessageRepository> _logger;
    
    public MessageRepository(IDbContextFactory<SaturnContext> dbContextFactory, ILogger<MessageRepository> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }
    public async Task<List<MessageEntity>> GetMessageChainAsync(long chatId, long messageId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        
        var currentMessage = await dbContext.Messages
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == messageId && m.ChatId == chatId);

        if (currentMessage == null)
           return [];

        var chain = new List<MessageEntity> { currentMessage };

        while (currentMessage is { ReplyToMessageId: not null, ReplyToMessageChatId: not null })
        {
            currentMessage = await dbContext.Messages
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.Id == currentMessage.ReplyToMessageId.Value &&
                                          m.ChatId == currentMessage.ReplyToMessageChatId.Value);
        
            if (currentMessage == null)
                break;
            
            chain.Add(currentMessage);
        }
    
        return chain;

    }

    public async Task<List<MessageEntity>> SearchMessagesAsync(
        long chatId,
        long? userId,
        DateTime? from,
        DateTime? to,
        string? text,
        int limit,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = dbContext.Messages
            .Include(m => m.User)
            // The bot's own past answers are excluded so the model doesn't treat its earlier inventions as chat facts
            .Where(m => m.ChatId == chatId && !m.IsBot && m.Text != null);

        if (userId != null)
            query = query.Where(m => m.UserId == userId);

        if (from != null)
            query = query.Where(m => m.MessageDate >= from);

        if (to != null)
            query = query.Where(m => m.MessageDate < to);

        if (string.IsNullOrWhiteSpace(text))
            return await TakeLatestAsync(query, limit, cancellationToken);

        // Full-text search handles Russian morphology ("машина" finds "машину"), but misses word fragments,
        // slang and transliteration — fall back to a plain substring match when it finds nothing
        var trimmedText = text.Trim();
        var fullTextMatches = await TakeLatestAsync(
            query.Where(m => m.SearchVector!.Matches(EF.Functions.WebSearchToTsQuery("russian", trimmedText))),
            limit,
            cancellationToken);
        if (fullTextMatches.Count > 0)
        {
            _logger.LogInformation("Message search for \"{Text}\": full-text found {Count}", trimmedText, fullTextMatches.Count);
            return fullTextMatches;
        }

        var substringPattern = $"%{EscapeLikePattern(trimmedText)}%";
        var substringMatches = await TakeLatestAsync(
            query.Where(m => EF.Functions.ILike(m.Text!, substringPattern, LikeEscapeCharacter)),
            limit,
            cancellationToken);
        _logger.LogInformation("Message search for \"{Text}\": full-text found nothing, substring fallback found {Count}",
            trimmedText, substringMatches.Count);
        return substringMatches;
    }

    public async Task<List<ChatAuthor>> GetChatAuthorsAsync(long chatId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Messages
            .Where(m => m.ChatId == chatId && m.User != null)
            .GroupBy(m => new { m.UserId, m.User!.Username, m.User.FirstName, m.User.LastName })
            .OrderByDescending(g => g.Count())
            .Select(g => new ChatAuthor(g.Key.UserId, g.Key.Username, g.Key.FirstName, g.Key.LastName, g.Count()))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PopularSticker>> GetPopularStickersAsync(long chatId, int limit, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.Messages
            .Where(m => m.ChatId == chatId && m.StickerId != null && !m.IsBot)
            .GroupBy(m => m.StickerId!)
            .OrderByDescending(g => g.Count())
            .Take(limit)
            .Select(g => new PopularSticker(g.Key, g.Max(m => m.StickerEmoji), g.Count()))
            .ToListAsync(cancellationToken);
    }

    public async Task SetStickerEmojiAsync(string stickerId, string emoji, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        await dbContext.Messages
            .Where(m => m.StickerId == stickerId && m.StickerEmoji == null)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.StickerEmoji, emoji), cancellationToken);
    }

    /// <summary>Takes the most recent messages and returns them in chronological order.</summary>
    private static async Task<List<MessageEntity>> TakeLatestAsync(
        IQueryable<MessageEntity> query,
        int limit,
        CancellationToken cancellationToken)
    {
        var messages = await query
            .OrderByDescending(m => m.MessageDate)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return messages.OrderBy(m => m.MessageDate).ToList();
    }

    // Npgsql emits ESCAPE '' unless the escape character is passed explicitly
    private const string LikeEscapeCharacter = @"\";

    private static string EscapeLikePattern(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
