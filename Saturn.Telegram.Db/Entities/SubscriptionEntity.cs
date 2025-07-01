namespace Saturn.Telegram.Db.Entities;

public class SubscriptionEntity
{
    public Guid Id { get; set; }

    public long UserId { get; set; }
    
    public DateTime ValidUntil { get; set; }
    
    public DateTime Date { get; set; }

    public SubscriptionType Type { get; set; }
    public virtual UserEntity User { get; set; }
}