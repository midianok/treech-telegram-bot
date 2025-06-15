using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Saturn.Bot.Service.Operations;

public class PaymentOperation : OperationBase
{
    protected override Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        return type switch
        {
            UpdateType.Message when msg.Text!.StartsWith("/payment") => Process(msg, type)
        };
    }

    private async Task Process(Message msg, UpdateType type)
    {
        var chatId = msg.Chat.Id;
        var keyboard = new InlineKeyboardMarkup( new[]
        {
            InlineKeyboardButton.WithCallbackData("На неделю", $"ch_gn_7_{chatId}"),
            InlineKeyboardButton.WithCallbackData("На на месяц", $"ch_gn_31_{chatId}"),
            
        });
        
        await TelegramBotClient.SendMessage(msg.Chat, "Платные подписки", ParseMode.MarkdownV2, replyMarkup: keyboard);
    }
    protected override Task ProcessOnUpdateAsync(Update update)
    {
        return update.Type switch
        {
            UpdateType.CallbackQuery when update.CallbackQuery!.Message.Chat.Type == ChatType.Private && update.CallbackQuery!.Data.StartsWith("ch_gn_7_") => ProcessUpdate(update)
        };
    }

    private async Task ProcessUpdate(Update update)
    {
        //await TelegramBotClient.SendMessage(msg.Chat, "Отдохни ещё чутка", ParseMode.MarkdownV2, replyMarkup: InlineKeyboardButton.WithUrl("Убрать задержку на неделю", t));
        await TelegramBotClient.EditMessageText(update.CallbackQuery!.Message.Chat, update.CallbackQuery!.Message.Id, "Платные подписки", ParseMode.MarkdownV2, replyMarkup: InlineKeyboardButton.WithCallbackData("На на месяц", $"ch_gn_31_"));
    }

    protected override bool ValidateOnTextMessage(Message msg, UpdateType type) => true;
}