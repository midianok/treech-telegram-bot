using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Lib.Operation;

public interface IOperation
{
    bool Validate(Message msg, UpdateType type);
    
    Task OnMessageAsync(Message msg, UpdateType type);
    
    Task OnUpdateAsync(Update update);
}
