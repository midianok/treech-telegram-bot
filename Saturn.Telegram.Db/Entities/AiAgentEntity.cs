namespace Saturn.Telegram.Db.Entities;

public class AiAgentEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    public string Code { get; set; }
    
    public string Prompt { get; set; }

    public virtual List<ChatEntity> Chats { get; set; }
    
}