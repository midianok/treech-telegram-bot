using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations;

public class ShowChatLinkOperation : OperationBase
{

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var chat = await TelegramBotClient.GetChat(msg.Chat);
        if (string.IsNullOrEmpty(chat.InviteLink))
        {
            return;
        }
        await TelegramBotClient.SendMessage(msg.Chat, $"🔗*Ссылка на чат*: `{chat.InviteLink}`", ParseMode.MarkdownV2);
        await TelegramBotClient.SendMessage(msg.Chat, chat.InviteLink);
    }

    protected override bool ValidateOnMessage(Message msg, UpdateType type) =>
        type == UpdateType.Message &&
        !string.IsNullOrEmpty(msg.Text) &&
        msg.Text.Contains("ссылк", StringComparison.CurrentCultureIgnoreCase);
}