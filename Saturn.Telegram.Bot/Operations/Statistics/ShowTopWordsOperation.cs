using Microsoft.EntityFrameworkCore;
using Saturn.Bot.Service.Extensions;
using Saturn.Bot.Service.Operations.Ai;
using Saturn.Telegram.Db;
using Saturn.Telegram.Lib.Operation;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Statistics;

public class ShowTopWordsOperation : IOperation
{
    private const int LookbackDays = 90;

    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    public ShowTopWordsOperation(ITelegramBotClient telegramBotClient, IDbContextFactory<SaturnContext> contextFactory)
    {
        _telegramBotClient = telegramBotClient;
        _contextFactory = contextFactory;
    }

    public bool Validate(Message msg, UpdateType type) =>
        msg.HasText("топ слов");

    public async Task OnMessageAsync(Message msg, UpdateType type, CancellationToken сancellationToken)
    {
        var targetUser = msg.ReplyToMessage?.From ?? msg.From;
        if (targetUser == null || targetUser.IsBot) return;

        await using var db = await _contextFactory.CreateDbContextAsync(сancellationToken);

        var since = DateTime.UtcNow.Date.AddDays(-LookbackDays);

        var texts = await db.Messages
            .Where(x => x.ChatId == msg.Chat.Id &&
                        x.UserId == targetUser.Id &&
                        !x.IsBot &&
                        x.MessageDate >= since &&
                        x.Text != null)
            .Select(x => x.Text!)
            .ToListAsync(сancellationToken);

        if (texts.Count == 0)
        {
            await _telegramBotClient.SendMessage(
                msg.Chat,
                "нет сообщений за последние 3 месяца",
                replyParameters: new ReplyParameters { MessageId = msg.Id }, cancellationToken: сancellationToken);
            return;
        }

        var topWords = SilenceOperation.CountTopWords(texts);
        if (topWords.Count == 0)
        {
            await _telegramBotClient.SendMessage(
                msg.Chat,
                "не удалось найти значимые слова",
                replyParameters: new ReplyParameters { MessageId = msg.Id }, cancellationToken: сancellationToken);
            return;
        }

        var displayName = !string.IsNullOrEmpty(targetUser.Username)
            ? $"@{targetUser.Username}"
            : $"{targetUser.FirstName} {targetUser.LastName}".Trim();

        var sb = new StringBuilder($"Топ слов {displayName} за 3 месяца:\n\n");
        for (var i = 0; i < Math.Min(10, topWords.Count); i++)
            sb.AppendLine($"{i + 1}. {topWords[i].Word} — {topWords[i].Count}");

        await _telegramBotClient.SendMessage(
            msg.Chat,
            sb.ToString().TrimEnd(),
            ParseMode.None,
            replyParameters: new ReplyParameters { MessageId = msg.Id }, cancellationToken: сancellationToken);
    }
}
