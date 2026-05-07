namespace Saturn.Telegram.Db.Entities;

public class AiAgentEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Prompt { get; set; } = null!;

    public virtual List<ChatEntity>? Chats { get; set; }
    
}