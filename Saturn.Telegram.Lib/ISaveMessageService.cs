using Telegram.Bot.Types;

namespace Saturn.Telegram.Lib;

public interface ISaveMessageService
{
    Task SaveMessageAsync(Message msg);
}
