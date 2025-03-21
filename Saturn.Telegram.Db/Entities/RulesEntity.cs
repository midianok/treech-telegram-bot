namespace Saturn.Telegram.Db.Entities;

public class RulesEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    
    public Guid ChatId { get; set; }

    public int RuleType { get; set; }

    public required string Subject { get; set; }

    public string? Message { get; set; }
}