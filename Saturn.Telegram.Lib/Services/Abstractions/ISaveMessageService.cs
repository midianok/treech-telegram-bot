using Telegram.Bot.Types;

namespace Saturn.Telegram.Lib.Services.Abstractions;

public interface ISaveMessageService
{
    Task SaveMessageAsync(Message msg);
}