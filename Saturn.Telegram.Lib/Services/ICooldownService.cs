namespace Saturn.Telegram.Lib.Services;

public interface ICooldownService
{
    Task<bool> InCooldown(long chatId, long userId);
}