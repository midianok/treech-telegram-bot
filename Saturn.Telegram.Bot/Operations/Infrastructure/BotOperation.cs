using Saturn.Bot.Service.Extensions;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Saturn.Bot.Service.Operations.Infrastructure;

public class BotOperation(TelegramBotClient telegramBotClient) : IOperation
{
    
    public bool Validate(Message msg, UpdateType type) => 
        msg.HasText("бот");

    public Task OnMessageAsync(Message msg, UpdateType type)
    {
        var keyboard = new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl("Открыть приложение", $"https://t.me/TreechBot/app?startapp={msg.Chat.Id}"));
        return telegramBotClient.SendMessage(msg.Chat, "Приложение", ParseMode.None, new ReplyParameters { MessageId = msg.Id }, replyMarkup: keyboard);
    }
}