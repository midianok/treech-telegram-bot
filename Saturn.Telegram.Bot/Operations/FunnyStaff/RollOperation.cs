using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.FunnyStaff;

public class RollOperation : IOperation
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly Random _random = new();

    public RollOperation(TelegramBotClient telegramBotClient)
    {
        _telegramBotClient = telegramBotClient;
    }

    public bool Validate(Message msg, UpdateType type) =>
        !string.IsNullOrEmpty(msg.Text) && msg.Text.StartsWith("на дабл", StringComparison.CurrentCultureIgnoreCase);

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        var value = _random.Next(10, 99);
        await _telegramBotClient.SendMessage(msg.Chat, $"Ты выбросил *{value}*", ParseMode.MarkdownV2, new ReplyParameters { MessageId = msg.Id });
    }
}
