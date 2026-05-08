using Saturn.Bot.Service.Extensions;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Saturn.Bot.Service.Operations.FunnyStaff;

public class NamorevoGoreOperation(TelegramBotClient telegramBotClient) : IOperation
{
    public bool Validate(Message msg, UpdateType type) =>
        !string.IsNullOrEmpty(msg.Text) &&
        msg.HasText("наморево горе") || msg.HasText("наморово горе");

    public Task OnMessageAsync(Message msg, UpdateType type)
    {
        var keyboard = new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl("Наморево горе", "https://t.me/TreechBot/namorevogore"));
        return telegramBotClient.SendMessage(msg.Chat, "Наморево горе!", ParseMode.None, new ReplyParameters { MessageId = msg.Id }, replyMarkup: keyboard);
    }
}
