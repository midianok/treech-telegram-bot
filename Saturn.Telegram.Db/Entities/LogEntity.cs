namespace Saturn.Telegram.Db.Entities;

public class LogEntity
{
    public long Id { get; set; }
    public string Source { get; set; } = null!;
    public string Level { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string Data { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}
