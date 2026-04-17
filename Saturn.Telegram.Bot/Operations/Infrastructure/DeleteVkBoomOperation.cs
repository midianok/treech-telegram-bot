using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Infrastructure;

public class DeleteVkBoomOperation(TelegramBotClient telegramBotClient) : IOperation
{
    private const string VkBoomUsername = "vkBOOMrobot";

    public bool Validate(Message msg, UpdateType type)
    {
        if (msg.From?.Username != VkBoomUsername)
            return false;

        return msg.Video == null && msg.Audio == null;
    }

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        await telegramBotClient.DeleteMessage(msg.Chat, msg.Id);
    }
}
