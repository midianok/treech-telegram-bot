using Microsoft.Extensions.Options;
using Saturn.Bot.Service.Extensions;
using Saturn.Bot.Service.Options;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Saturn.Bot.Service.Operations.Infrastructure;

public class BotOperation(ITelegramBotClient telegramBotClient, IOptions<BotOptions> botOptions) : IOperation
{
    public bool Validate(Message msg, UpdateType type) =>
        msg.HasText("бот");

    public Task OnMessageAsync(Message msg, UpdateType type)
    {
        var keyboard = new InlineKeyboardMarkup(InlineKeyboardButton.WithUrl("Открыть", $"https://t.me/{botOptions.Value.BotUsername}/app?startapp={msg.Chat.Id}"));
        return telegramBotClient.SendMessage(msg.Chat, "Treech App", replyMarkup: keyboard);
    }
}
