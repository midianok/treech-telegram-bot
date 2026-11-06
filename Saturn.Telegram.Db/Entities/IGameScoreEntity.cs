namespace Saturn.Telegram.Db.Entities;

/// <summary>
/// Лучший счёт пользователя в мини-игре в рамках чата.
/// </summary>
public interface IGameScoreEntity
{
    long UserId { get; set; }

    long ChatId { get; set; }

    int Score { get; set; }

    UserEntity? User { get; set; }
}
