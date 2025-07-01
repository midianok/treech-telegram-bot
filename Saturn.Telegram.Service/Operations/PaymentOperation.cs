using Microsoft.Extensions.Logging;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.Payments;
using Telegram.Bot.Types.ReplyMarkups;

namespace Saturn.Bot.Service.Operations;

public class PaymentOperation : OperationBase
{
    private readonly ILogger<PaymentOperation> _logger;

    public PaymentOperation(ILogger<PaymentOperation> logger)
    {
        _logger = logger;
    }

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        _logger.LogInformation("Received Payment Operation");
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
            "sub30" => "30",
            _ => throw new ArgumentOutOfRangeException()
        };
        
        var cost = update.CallbackQuery!.Data switch
        {
            "sub7" => 30,
            "sub30" => 100,
            _ => throw new ArgumentOutOfRangeException()
        };
        
        await TelegramBotClient.SendInvoice(update.CallbackQuery!.Message!.Chat, "Оплата подписки", $"Убрать задержку на {days} дней", update.CallbackQuery!.Data, "XTR", [new LabeledPrice($"Убрать задержку!!!!! на {days} дней", cost)]);
        
        await TelegramBotClient.AnswerCallbackQuery(update.CallbackQuery.Id);
    }
    
    private async Task ProcessPayment(Update update)
    {
        try
        {
            var validUntil = update.PreCheckoutQuery!.InvoicePayload switch
            {
                "sub7" => DateTime.Now.AddDays(7),
                "sub30" => DateTime.Now.AddDays(30),
                _ => throw new ArgumentOutOfRangeException()
            };

            if (await SubscriptionService.HasSubscriptionAsync(update.PreCheckoutQuery!.From.Id, SubscriptionType.RemoveChatCooldown))
            {
                await TelegramBotClient.AnswerPreCheckoutQuery(update.PreCheckoutQuery!.Id, "У вас уже есть активная подписка");
                return;
            }
            
            await SubscriptionService.AddSubscriptionAsync(update.PreCheckoutQuery!.From.Id, validUntil, SubscriptionType.RemoveChatCooldown);
            await TelegramBotClient.AnswerPreCheckoutQuery(update.PreCheckoutQuery!.Id);
        }
        catch (Exception exception)
        {
            Logger.LogError("Error: {exceptionMessage}", exception.Message);
            await TelegramBotClient.AnswerPreCheckoutQuery(update.PreCheckoutQuery!.Id, "Что-то пошло не так(");
            throw;
        }

    }

    protected override bool ValidateOnTextMessage(Message msg, UpdateType type)
    {
        _logger.LogInformation("Received Payment Operation {MsgText}", msg.Text);
        return msg.Text == "/start payment";
    }
}