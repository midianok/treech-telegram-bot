using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Lib.Operation;

public interface IOperation
{
    bool Enabled();

    Task OnMessageAsync(Message msg, UpdateType type);

    Task OnUpdateAsync(Update update);

    Task OnErrorAsync(Exception exception, HandleErrorSource source);
}