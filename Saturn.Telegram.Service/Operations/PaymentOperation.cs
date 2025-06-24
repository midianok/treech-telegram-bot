using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.Payments;
using Telegram.Bot.Types.ReplyMarkups;

namespace Saturn.Bot.Service.Operations;

public class PaymentOperation : OperationBase
{
    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var keyboard = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData("На 7 дней ", "sub7"), InlineKeyboardButton.WithCallbackData("На 30 дней", "sub30"));
        
        await TelegramBotClient.SendMessage(msg.Chat, "Платные подписки", ParseMode.MarkdownV2, replyMarkup: keyboard);
    }
    
    protected override Task ProcessOnUpdateAsync(Update update)
    {
        return update.Type switch
        {
            UpdateType.CallbackQuery when update.CallbackQuery!.Message!.Chat.Type == ChatType.Private && update.CallbackQuery!.Data!.StartsWith("sub") => SendInvoice(update),
            UpdateType.PreCheckoutQuery => ProcessPayment(update)
        };
    }

    private async Task SendInvoice(Update update)
    {
        var days = update.CallbackQuery!.Data switch
        {
            "sub7" => "7",
            "sub30" => "30"
        };
        
        await TelegramBotClient.SendInvoice(update.CallbackQuery!.Message!.Chat, "Оплата подписки", $"Убрать задержку на {days} дней", update.CallbackQuery!.Data, "XTR", [new LabeledPrice($"Убрать задержку!!!!! на {days} дней", 1)]);
        
        await TelegramBotClient.AnswerCallbackQuery(update.CallbackQuery.Id);
    }
    
    private async Task ProcessPayment(Update update)
    {
        //todo:обработка подписки
        await TelegramBotClient.AnswerPreCheckoutQuery(update.PreCheckoutQuery!.Id);
    }

    protected override bool ValidateOnTextMessage(Message msg, UpdateType type) => 
        msg.Text == "/start payment";
}