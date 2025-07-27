namespace Saturn.Telegram.Db.Entities;

public class ChatEntity
{
    public long Id { get; set; }
    
    public int Type { get; set; }
    
    public string? Name { get; set; }
    
    public Guid? AiAgentId { get; set; }
    
    public virtual List<MessageEntity>? Messages { get; set; }
    
    public virtual AiAgentEntity? AiAgent { get; set; }
}