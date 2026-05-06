namespace Saturn.Telegram.Db.Entities;

public class KarmaChangeEntity
{
    public long Id { get; set; }

    public long FromUserId { get; set; }

    public long ToUserId { get; set; }

    public long ChatId { get; set; }

    public long MessageId { get; set; }

    public int Delta { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual UserEntity? FromUser { get; set; }

    public virtual UserEntity? ToUser { get; set; }
}
