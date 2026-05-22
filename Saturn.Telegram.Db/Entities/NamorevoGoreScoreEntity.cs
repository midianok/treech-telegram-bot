namespace Saturn.Telegram.Db.Entities;

public class NamorevoGoreScoreEntity
{
    public long UserId { get; set; }

    public long ChatId { get; set; }

    public int Score { get; set; }

    public virtual UserEntity? User { get; set; }
}