namespace Saturn.Telegram.Db.Entities;

public class ImagePromptEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Keywords { get; set; } = null!;
    public string Prompt { get; set; } = null!;
}
