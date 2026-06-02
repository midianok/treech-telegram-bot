using Microsoft.EntityFrameworkCore;

namespace Saturn.Telegram.Db.CacheInvalidation;

public class CacheInvalidator(IDbContextFactory<SaturnContext> contextFactory) : ICacheInvalidator
{
    public async Task InvalidateAgentAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            $"SELECT pg_notify('{CacheInvalidationChannels.Agent}', {{0}})",
            [agentId.ToString()]);
    }

    public async Task InvalidateChatAsync(long chatId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            $"SELECT pg_notify('{CacheInvalidationChannels.Chat}', {{0}})",
            [chatId.ToString()]);
    }

    public async Task InvalidateImagePromptsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            $"SELECT pg_notify('{CacheInvalidationChannels.ImagePrompt}', '')",
            cancellationToken: cancellationToken);
    }
}
