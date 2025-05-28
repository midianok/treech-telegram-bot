namespace Saturn.Telegram.Db.Entities;

public class MessageEntity
{
    public long Id { get; set; }

    public long ChatId { get; set; }
    
    public long UserId { get; set; }

    public virtual UserEntity? User { get; set; }
    
    public virtual ChatEntity? Chat { get; set; }
    
    public int Type { get; set; }

    public string? Text { get; set; }

    public string? StickerId { get; set; }

    public DateTime MessageDate { get; set; }
}