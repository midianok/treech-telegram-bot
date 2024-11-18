using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Abstractions;

public interface IOperation
{
    Task OnMessageAsync(Message msg, UpdateType type);

    Task OnUpdateAsync(Update update);

    Task OnErrorAsync(Exception exception, HandleErrorSource source);
}