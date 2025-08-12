using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Db.Repositories.Abstractions;

namespace Saturn.Telegram.Db.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly IDbContextFactory<SaturnContext> _dbContextFactory;
    
    public MessageRepository(IDbContextFactory<SaturnContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }
    public async Task<List<MessageEntity>> GetMessageChainAsync(long chatId, long messageId)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        
        var currentMessage = await dbContext.Messages
            .FirstOrDefaultAsync(m => m.Id == messageId && m.ChatId == chatId);
    
        if (currentMessage == null)
           return [];
        
        var chain = new List<MessageEntity> { currentMessage };

        while (currentMessage is { ReplyToMessageId: not null, ReplyToMessageChatId: not null })
        {
            currentMessage = await dbContext.Messages
                .FirstOrDefaultAsync(m => m.Id == currentMessage.ReplyToMessageId.Value && 
                                          m.ChatId == currentMessage.ReplyToMessageChatId.Value);
        
            if (currentMessage == null)
                break;
            
            chain.Add(currentMessage);
        }
    
        return chain;

    }
}