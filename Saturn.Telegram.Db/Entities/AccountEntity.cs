namespace Saturn.Telegram.Db.Entities;

public class AccountEntity
{
    public Guid Id { get; set; }

    public long ChatId { get; set; }

    public long UserId { get; set; }

    public decimal Balance { get; set; }
}