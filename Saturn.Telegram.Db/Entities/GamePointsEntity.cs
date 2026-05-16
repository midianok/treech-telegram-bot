namespace Saturn.Telegram.Db.Entities;

public class GamePointsEntity
{
    public long UserId { get; set; }

    public long ChatId { get; set; }

    public int Points { get; set; }

    public virtual UserEntity? User { get; set; }
}
