using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Lib.Abstractions;

public interface IOperationManager
{
    Task MessageHandler(Message msg, UpdateType type);
    Task UpdateHandler(Update update);
    Task ErrorHandler(Exception exception, HandleErrorSource source);
}
