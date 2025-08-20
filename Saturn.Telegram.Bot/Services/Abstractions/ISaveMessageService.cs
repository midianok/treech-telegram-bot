using Telegram.Bot.Types;

namespace Saturn.Bot.Service.Services.Abstractions;

public interface ISaveMessageService
{
    Task SaveMessageAsync(Message msg);
}