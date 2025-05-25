namespace Saturn.Telegram.Lib.Services;

public interface ICooldownService
{
    Task<(bool cooldown, string? CooldownMessage)> IfInCooldown(string operationType, long chatId, long userId);
    
    Task SetCooldown(string operationType, long chatId, long userId);
}