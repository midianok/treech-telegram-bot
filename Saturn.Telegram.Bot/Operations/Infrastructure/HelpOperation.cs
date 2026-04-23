using Saturn.Bot.Service.Extensions;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Infrastructure;

public class HelpOperation(TelegramBotClient telegramBotClient) : IOperation
{
    private readonly string _helpText = File
        .ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Help.md"))
        .EscapeMarkdownV2();

    public bool Validate(Message msg, UpdateType type) =>
        msg.HasText("помощь");

    public Task OnMessageAsync(Message msg, UpdateType type) =>
        telegramBotClient.SendMessage(msg.Chat, _helpText, ParseMode.MarkdownV2,
            new ReplyParameters { MessageId = msg.Id });

    public Task OnUpdateAsync(Update update) => Task.CompletedTask;
}
