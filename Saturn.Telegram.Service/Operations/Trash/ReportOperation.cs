using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Saturn.Bot.Service.Operations;

public class ReportOperation : OperationBase
{
    private const long ReportChatId = -1001864826490;
    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        await TelegramBotClient.SendMessage(ReportChatId,
            "REPORT: @nkess @qwrzlp @trofimovng @CptDragynov @ilya\\_naprimer",
            ParseMode.MarkdownV2,
            replyMarkup: InlineKeyboardButton.WithUrl("MESSAGE", $"https://t.me/c/1536029449/{msg.ReplyToMessage!.MessageId}"));
        
        await TelegramBotClient.SendMessage(msg.Chat, "Отчёт отправлен");
    }  

    protected override bool ValidateOnTextMessage(Message msg, UpdateType type) =>
        type == UpdateType.Message &&
        !string.IsNullOrEmpty(msg.Text) &&
        msg.ReplyToMessage != null &&
        msg.Text.ToLower().StartsWith("/report");
}