using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Db;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramUser = Telegram.Bot.Types.User;

namespace Saturn.Bot.Service.Operations.Karma;

public class ShowKarmaOperation : IOperation
{
    private const string ShowKarmaMessage = "карма";

    private readonly TelegramBotClient _telegramBotClient;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    public ShowKarmaOperation(TelegramBotClient telegramBotClient, IDbContextFactory<SaturnContext> contextFactory)
    {
        _telegramBotClient = telegramBotClient;
        _contextFactory = contextFactory;
    }

    public bool Validate(Message msg, UpdateType type) =>
        Normalize(msg.Text) == ShowKarmaMessage;

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        var targetUser = msg.ReplyToMessage?.From ?? msg.From;
        if (targetUser == null)
        {
            return;
        }

        await using var db = await _contextFactory.CreateDbContextAsync();
        var karma = await db.UserKarma
            .Where(x => x.UserId == targetUser.Id)
            .Select(x => x.Value)
            .FirstOrDefaultAsync();

        await _telegramBotClient.SendMessage(
            msg.Chat,
            $"Карма {FormatUser(targetUser)}: {karma}",
            replyParameters: new ReplyParameters { MessageId = msg.Id });
    }

    private static string? Normalize(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim().ToLowerInvariant();

    private static string FormatUser(TelegramUser user)
    {
        if (!string.IsNullOrWhiteSpace(user.Username))
        {
            return $"@{user.Username}";
        }

        var fullName = string.Join(' ', new[] { user.FirstName, user.LastName }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        return string.IsNullOrWhiteSpace(fullName) ? user.Id.ToString() : fullName;
    }
}
