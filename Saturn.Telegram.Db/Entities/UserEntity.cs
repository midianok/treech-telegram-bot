namespace Saturn.Telegram.Db.Entities;

public class UserEntity
{
    public long Id { get; set; }

    public string? Username { get; set; }

    public string? FirstName { get; set; }
    
    public string? LastName { get; set; }

    /// <summary>Internal currency balance ("искры"). Topped up via payments, spent on paid operations.</summary>
    public long CoinBalance { get; set; }

    public virtual List<MessageEntity>? Messages { get; set; }
}