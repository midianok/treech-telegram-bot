using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Repositories.Abstractions;

public interface IReminderRepository
{
    Task AddAsync(ReminderEntity reminder, CancellationToken cancellationToken);

    /// <summary>How many not yet sent reminders <paramref name="createdByUserId"/> has set in the chat.</summary>
    Task<int> CountPendingAsync(long chatId, long createdByUserId, CancellationToken cancellationToken);

    /// <summary>Pending reminders whose time has come, oldest first, with both users loaded.</summary>
    Task<List<ReminderEntity>> GetDueAsync(DateTime now, int limit, CancellationToken cancellationToken);

    Task MarkSentAsync(long reminderId, DateTime sentAt, CancellationToken cancellationToken);

    /// <summary>Pending reminders in the chat that <paramref name="userId"/> set or is reminded by, soonest first, with both users loaded.</summary>
    Task<List<ReminderEntity>> GetPendingForUserAsync(long chatId, long userId, CancellationToken cancellationToken);

    /// <summary>A pending reminder in the chat, with both users loaded; null if it doesn't exist or was already sent.</summary>
    Task<ReminderEntity?> GetPendingAsync(long chatId, long reminderId, CancellationToken cancellationToken);

    Task DeleteAsync(long reminderId, CancellationToken cancellationToken);
}
