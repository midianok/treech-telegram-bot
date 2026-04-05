using Saturn.Telegram.Lib.Operation;
using Telegram.Bot.Types;

namespace Saturn.Telegram.Lib.Infrastructure;

public interface ICooldownService
{
    Task<bool> IsCooldownAsync(IOperation operation, Message msg);
    void SetCooldown(IOperation operation, Message msg);
}
