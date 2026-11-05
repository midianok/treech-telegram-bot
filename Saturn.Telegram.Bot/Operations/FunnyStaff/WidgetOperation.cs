using Microsoft.Extensions.Options;
using Saturn.Bot.Service.Extensions;
using Saturn.Bot.Service.Infrastructure.TelegramWidgetClient;
using Saturn.Bot.Service.Options;
using Saturn.Telegram.Lib.Attributes;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.FunnyStaff;

/// <summary>
/// "/widget" — sends the runner mini-game as a Telegram widget (Bot API 10.4 <c>sendWidget</c>).
/// </summary>
[Cooldown(10)]
public class WidgetOperation(
    ITelegramBotClient telegramBotClient,
    TelegramWidgetClient widgetClient,
    IOptions<BotOptions> botOptions) : IOperation
{
    private const string Command = "/widget";

    private static readonly Lazy<string> RunnerHtml = new(() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Media", "Widgets", "runner.html")));

    // "/widget", or "/widget@BotUsername" in groups
    public bool Validate(Message msg, UpdateType type) =>
        msg.HasText(Command) ||
        (!string.IsNullOrEmpty(botOptions.Value.BotUsername) && msg.HasText($"{Command}@{botOptions.Value.BotUsername}"));

    public async Task OnMessageAsync(Message msg, UpdateType type, CancellationToken сancellationToken)
    {
        try
        {
            await widgetClient.SendWidgetAsync(new SendWidgetRequest
            {
                ChatId = msg.Chat.Id,
                Html = RunnerHtml.Value,
                Caption = "🏃 Раннер",
                MessageThreadId = msg.IsTopicMessage ? msg.MessageThreadId : null,
                ReplyParameters = new ReplyParametersDto { MessageId = msg.Id, AllowSendingWithoutReply = true }
            }, сancellationToken);
        }
        catch (TelegramWidgetException)
        {
            await telegramBotClient.SendMessage(
                msg.Chat,
                "не получилось отправить виджет",
                replyParameters: new ReplyParameters { MessageId = msg.Id },
                cancellationToken: сancellationToken);
            throw;
        }
    }
}
