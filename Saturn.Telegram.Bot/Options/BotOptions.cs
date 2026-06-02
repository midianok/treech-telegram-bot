namespace Saturn.Bot.Service.Options;

public class BotOptions
{
    public string BotUsername { get; set; } = string.Empty;
    
    public string InvokeCommand { get; set; } = string.Empty;
    
    public long LogChatId { get; set; }
    
    public string? AdminUsername { get; set; }
    
    public string? EasterEggUsername { get; set; }
}
