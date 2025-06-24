using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Saturn.Bot.Service.Operations;

public class SendInvoiceOperation : OperationBase
{
    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        await TelegramBotClient.SendMessage(msg.Chat, 
            "Отдохни ещё чутка", 
            ParseMode.MarkdownV2, 
            replyMarkup: InlineKeyboardButton.WithCallbackData("Убрать задержку на неделю", "qwqwqweqwe"));
        
        //  await TelegramBotClient.SendMessage(msg.Chat, 
        //      "Отдохни ещё чутка", 
        //      ParseMode.MarkdownV2, 
        //      replyMarkup: InlineKeyboardButton.WithCallbackData("Убрать задержку на неделю", "qwqwqweqwe"));
        //
        // var t = await TelegramBotClient.CreateInvoiceLink("Отдохни ещё", "Отдохни ещё", $"testset_{msg.Chat.Id}", "XTR", [new LabeledPrice("Убрать задержку на неделю", 1)]);
        // await TelegramBotClient.SendMessage(msg.Chat, "Отдохни ещё чутка", ParseMode.MarkdownV2, replyMarkup: InlineKeyboardButton.WithUrl("Убрать задержку на неделю", t));
        // await TelegramBotClient.SendInvoice(msg.Chat.Id, "Отдохни ещё", "Отдохни ещё", $"ChatGeneration_{msg.Chat.Id}", "XTR", [ new LabeledPrice("Убрать задержку на неделю", 1) ]);
    }

    protected override async Task ProcessOnUpdateAsync(Update update)
    {
        //await TelegramBotClient.SendMessage(update.Message.Chat, "пук");
        
        // switch (update.Type)
        // {
        //     case UpdateType.PreCheckoutQuery:
        //         await TelegramBotClient.AnswerPreCheckoutQuery(update.PreCheckoutQuery.Id);
        //         break;
        //     default:
        //         throw new ArgumentOutOfRangeException();
        // }
    }

    protected override bool ValidateOnTextMessage(Message msg, UpdateType type) => false;
}