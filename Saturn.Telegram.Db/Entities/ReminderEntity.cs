namespace Saturn.Telegram.Db.Entities;

/// <summary>A reminder the AI chat promised to post in a chat at a given time.</summary>
public class ReminderEntity
{
    public long Id { get; set; }

    public long ChatId { get; set; }

    /// <summary>Who gets reminded (mentioned in the reminder).</summary>
    public long UserId { get; set; }

    /// <summary>Who asked for the reminder; may be the same person as <see cref="UserId"/>.</summary>
    public long CreatedByUserId { get; set; }

    /// <summary>Message the reminder was requested in; the reminder replies to it.</summary>
    public long? RequestMessageId { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTime RemindAt { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>When the reminder was posted (or given up on); null while it is pending.</summary>
    public DateTime? SentAt { get; set; }

    public virtual UserEntity? User { get; set; }

    public virtual UserEntity? CreatedByUser { get; set; }
}
