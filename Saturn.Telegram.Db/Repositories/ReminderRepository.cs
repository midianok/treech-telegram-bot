using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Db.Repositories.Abstractions;

namespace Saturn.Telegram.Db.Repositories;

public class ReminderRepository : IReminderRepository
{
    private readonly IDbContextFactory<SaturnContext> _dbContextFactory;

    public ReminderRepository(IDbContextFactory<SaturnContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task AddAsync(ReminderEntity reminder, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountPendingAsync(long chatId, long createdByUserId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Reminders.CountAsync(
            r => r.ChatId == chatId && r.CreatedByUserId == createdByUserId && r.SentAt == null,
            cancellationToken);
    }

    public async Task<List<ReminderEntity>> GetDueAsync(DateTime now, int limit, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Reminders
            .Include(r => r.User)
            .Include(r => r.CreatedByUser)
            .Where(r => r.SentAt == null && r.RemindAt <= now)
            .OrderBy(r => r.RemindAt)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task MarkSentAsync(long reminderId, DateTime sentAt, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await db.Reminders
            .Where(r => r.Id == reminderId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.SentAt, sentAt), cancellationToken);
    }

    public async Task<List<ReminderEntity>> GetPendingForUserAsync(long chatId, long userId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Reminders
            .Include(r => r.User)
            .Include(r => r.CreatedByUser)
            .Where(r => r.ChatId == chatId && r.SentAt == null && (r.CreatedByUserId == userId || r.UserId == userId))
            .OrderBy(r => r.RemindAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<ReminderEntity?> GetPendingAsync(long chatId, long reminderId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Reminders
            .Include(r => r.User)
            .Include(r => r.CreatedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reminderId && r.ChatId == chatId && r.SentAt == null, cancellationToken);
    }

    public async Task DeleteAsync(long reminderId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await db.Reminders
            .Where(r => r.Id == reminderId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
