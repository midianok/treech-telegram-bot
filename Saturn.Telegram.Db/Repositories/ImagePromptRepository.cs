using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Saturn.Telegram.Db.Repositories.Abstractions;

namespace Saturn.Telegram.Db.Repositories;

public class ImagePromptRepository : IImagePromptRepository
{
    private const string CacheKey = "ImagePrompts_All";

    private readonly IDbContextFactory<SaturnContext> _dbContextFactory;
    private readonly IMemoryCache _memoryCache;

    public ImagePromptRepository(IDbContextFactory<SaturnContext> dbContextFactory, IMemoryCache memoryCache)
    {
        _dbContextFactory = dbContextFactory;
        _memoryCache = memoryCache;
    }

    public async Task<string?> FindPromptAsync(string query)
    {
        var prompts = await _memoryCache.GetOrCreateAsync(CacheKey, async _ =>
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync();
            var all = await context.ImagePrompts.ToListAsync();
            return all
                .SelectMany(x => x.Keywords
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(kw => (kw, x.Prompt)))
                .ToDictionary(x => x.kw, x => x.Prompt);
        });

        if (prompts is null || prompts.Count == 0)
            return null;

        prompts.TryGetValue(query, out var prompt);
        return prompt;
    }

    public Task InvalidateAsync()
    {
        _memoryCache.Remove(CacheKey);
        return Task.CompletedTask;
    }
}
