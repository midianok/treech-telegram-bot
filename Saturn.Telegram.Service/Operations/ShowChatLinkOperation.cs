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

    protected override bool ValidateOnTextMessage(Message msg, UpdateType type) =>
        msg.Text!.Contains("ссылк", StringComparison.CurrentCultureIgnoreCase);

    protected override Task ProcessOnUpdateAsync(Update update)
    {
        return Task.CompletedTask;
    }
}

// protected override async Task ProcessOnUpdateAsync(Update update)
// {
//     switch (update.Type)
//     {
//         case UpdateType.Message:
//             var t = await TelegramBotClient.CreateInvoiceLink("Тайтл", "описание", "qwe", "XTR", [new LabeledPrice("Счётчик 1", 1)]);
//             await TelegramBotClient.EditMessageReplyMarkup(update.CallbackQuery.Message.Chat.Id, update.CallbackQuery.Message.MessageId,  InlineKeyboardButton.WithUrl("MESSAGE", t));
//             break;
//         case UpdateType.PreCheckoutQuery:
//             await TelegramBotClient.AnswerPreCheckoutQuery(update.PreCheckoutQuery!.Id);
//             break;
//         default:
//             throw new ArgumentOutOfRangeException();
//     }
// }
//
// protected override async Task OnCooldownAsync(Message msg, UpdateType type, string cooldownMessage)
// {
//     await TelegramBotClient.SendInvoice(msg.Chat.Id, string.Empty, cooldownMessage, $"ChatGeneration_{msg.Chat.Id}", "XTR", [ new LabeledPrice("Убрать задержку на неделю", 1) ]);
// }