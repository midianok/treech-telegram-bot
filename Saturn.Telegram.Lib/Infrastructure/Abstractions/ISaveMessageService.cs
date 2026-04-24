using Telegram.Bot.Types;

namespace Saturn.Telegram.Lib.Infrastructure.Abstractions;

public interface ISaveMessageService
{
    Task SaveMessageAsync(Message msg);
}
