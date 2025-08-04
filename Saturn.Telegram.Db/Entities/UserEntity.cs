namespace Saturn.Telegram.Db.Entities;

public class UserEntity
{
    public long Id { get; set; }

    public string? Username { get; set; }

    public string? FirstName { get; set; }
    
    public string? LastName { get; set; }
    
    public virtual List<MessageEntity> Messages { get; set; }
}