using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Db.Repositories.Abstractions;

namespace Saturn.Telegram.Db.Repositories;

public class ChatCachedRepository : IChatCachedRepository
{
    private readonly IDbContextFactory<SaturnContext> _dbContextFactory;
    private readonly IMemoryCache _memoryCache;

    public ChatCachedRepository(IDbContextFactory<SaturnContext> dbContextFactory, IMemoryCache memoryCache)
    {
        _dbContextFactory = dbContextFactory;
        _memoryCache = memoryCache;
    }

    public async Task<ChatEntity> GetAsync(long chatId)
    {
        var key = $"{nameof(ChatEntity)}:{chatId}";
        var result = await _memoryCache.GetOrCreateAsync(key, async _ =>
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            return await context.Chats.Include(x => x.AiAgent).SingleAsync(x => x.Id == chatId);
        });
        return result!;
    }
}