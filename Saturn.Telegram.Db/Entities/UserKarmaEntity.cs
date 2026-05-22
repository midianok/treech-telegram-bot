namespace Saturn.Telegram.Db.Entities;

public class UserKarmaEntity
{
    public long UserId { get; set; }

    public long ChatId { get; set; }

    public int Value { get; set; }

    public virtual UserEntity? User { get; set; }
}
