namespace Saturn.Bot.Service.Database.Entities;

public class MessageEntity
{
    public Guid Id { get; set; }

    public int Type { get; set; }

    public string? Text { get; set; }

    public string? StickerId { get; set; }

    public DateTime MessageDate { get; set; }

    public long ChatId { get; set; }

    public int ChatType { get; set; }

    public string? ChatName { get; set; }

    public long? FromUserId { get; set; }

    public string? FromUsername { get; set; }

    public string? FromFirstName { get; set; }

    public string? FromLastName { get; set; }

    public string UpdateData { get; set; }
}