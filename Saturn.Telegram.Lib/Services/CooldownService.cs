using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Db;

namespace Saturn.Telegram.Lib.Services;

public class CooldownService : ICooldownService
{
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    public CooldownService(IDbContextFactory<SaturnContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public Task<bool> InCooldown(long chatId, long userId)
    {
        throw new NotImplementedException();
    }
}