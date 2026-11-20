using System.Text;
using Microsoft.EntityFrameworkCore;
using Saturn.Bot.Service.Extensions;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Karma;

/// <summary>
/// «топ карма» — топ-10 пользователей текущего чата по карме.
/// </summary>
public class TopKarmaOperation(
    ITelegramBotClient telegramBotClient,
    IDbContextFactory<SaturnContext> contextFactory) : IOperation
{
    private const int TopSize = 10;

    public bool Validate(Message msg, UpdateType type) => msg.HasText("топ карма");

    public async Task OnMessageAsync(Message msg, UpdateType type, CancellationToken сancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(сancellationToken);

        var top = await db.UserKarma
            .Include(x => x.User)
            .Where(x => x.ChatId == msg.Chat.Id)
            .OrderByDescending(x => x.Value)
            .Take(TopSize)
            .ToListAsync(сancellationToken);

        var replyMessage = new StringBuilder();
        if (top.Count == 0)
        {
            replyMessage.Append("В этом чате ещё никто не менял карму.");
        }
        else
        {
            replyMessage.Append("Топ по карме:\n\n");
            for (var i = 0; i < top.Count; i++)
            {
                var userName = top[i].User?.GetSilentHtmlLink() ?? top[i].UserId.ToString();
                replyMessage.Append($"{GetPlace(i + 1)} {userName}: {top[i].Value}\n");
            }
        }

        await telegramBotClient.SendMessage(
            msg.Chat,
            replyMessage.ToString(),
            parseMode: ParseMode.Html,
            replyParameters: new ReplyParameters { MessageId = msg.Id },
            linkPreviewOptions: new LinkPreviewOptions { IsDisabled = true },
            cancellationToken: сancellationToken);
    }

    private static string GetPlace(int position) =>
        position switch
        {
            1 => "🥇",
            2 => "🥈",
            3 => "🥉",
            _ => $"{position}."
        };
}