namespace Saturn.Telegram.Db.Entities;

/// <summary>
/// Лучший счёт пользователя в игре «Мемный трич» в рамках чата.
/// </summary>
public class MemeTowerScoreEntity
{
    public long UserId { get; set; }

    public long ChatId { get; set; }

    public int Score { get; set; }

    public virtual UserEntity? User { get; set; }
}
