namespace Saturn.Telegram.Db.Entities;

public class CooldownEntity
{
    public Guid Id { get; set; }
    
    public long? UserId { get; set; }
    
    public long ChatId { get; set; }

    public string Operation { get; set; }
    
    public int CooldownSeconds { get; set; }
    
    public string? Message { get; set; }
}