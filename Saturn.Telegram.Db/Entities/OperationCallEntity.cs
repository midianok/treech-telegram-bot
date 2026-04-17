namespace Saturn.Telegram.Db.Entities;

public class OperationCallEntity
{
    public long Id { get; set; }
    public string OperationName { get; set; } = null!;
    public long ChatId { get; set; }
    public long UserId { get; set; }
    public DateTime CalledAt { get; set; }

}
