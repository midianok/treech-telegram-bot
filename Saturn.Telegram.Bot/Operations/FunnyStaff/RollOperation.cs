using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.FunnyStaff;

public class RollOperation : OperationBase
{
    private readonly Random _random = new();

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var value = _random.Next(10, 99);
        await TelegramBotClient.SendMessage(msg.Chat, $"Ты выбросил *{value}*", ParseMode.MarkdownV2, new ReplyParameters { MessageId = msg.Id } );
    }

    protected override bool ValidateMessage(Message msg, UpdateType type) =>
        !string.IsNullOrEmpty(msg.Text) && msg.Text!.StartsWith("на дабл", StringComparison.CurrentCultureIgnoreCase);
}