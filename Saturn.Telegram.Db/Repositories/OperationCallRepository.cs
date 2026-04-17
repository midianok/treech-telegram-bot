using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Db.Repositories.Abstractions;

namespace Saturn.Telegram.Db.Repositories;

public class OperationCallRepository : IOperationCallRepository
{
    private readonly IDbContextFactory<SaturnContext> _dbContextFactory;

    public OperationCallRepository(IDbContextFactory<SaturnContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task RecordAsync(string operationName, long chatId, long userId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        db.OperationCalls.Add(new OperationCallEntity
        {
            OperationName = operationName,
            ChatId = chatId,
            UserId = userId,
            CalledAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }
}
