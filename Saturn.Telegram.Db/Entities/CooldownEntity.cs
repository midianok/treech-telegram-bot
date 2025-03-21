namespace Saturn.Telegram.Db.Entities;

public class CooldownEntity
{
    public Guid Id { get; set; }
    
    public Guid UserId { get; set; }
    
    public Guid ChatId { get; set; }

    public required string Subject { get; set; }
    
    public int CooldownSeconds { get; set; }
    
    public string? Message { get; set; }
}