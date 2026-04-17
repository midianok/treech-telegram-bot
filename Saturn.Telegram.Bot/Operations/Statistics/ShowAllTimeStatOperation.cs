using System.Text;
using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Db;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Statistics;

public class ShowAllTimeStatOperation : IOperation
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    public ShowAllTimeStatOperation(TelegramBotClient telegramBotClient, IDbContextFactory<SaturnContext> contextFactory)
    {
        _telegramBotClient = telegramBotClient;
        _contextFactory = contextFactory;
    }

    public bool Validate(Message msg, UpdateType type) =>
        !string.IsNullOrEmpty(msg.Text) && msg.Text.ToLower() == "вся стата";

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var topUsersByMessageCount = await db.Messages
            .Where(x => x.ChatId == msg.Chat.Id)
            .GroupBy(x => x.UserId)
            .Select(x => new
            {
                UserId = x.Key,
                x.First().User!.Username,
                x.First().User!.FirstName,
                x.First().User!.LastName,
                MessageCount = x.Count()
            })
            .OrderByDescending(x => x.MessageCount)
            .Take(10).ToListAsync();

        var replyMessage = new StringBuilder("Топ по сообщениям за всё время:\n");
        var iterator = 1;

        foreach (var user in topUsersByMessageCount)
        {
            var userName = !string.IsNullOrEmpty(user.Username) ? $"@{user.Username}" : user.UserId.ToString();
            var emoji = GetEmoji(iterator++);
            replyMessage.Append($"{emoji} {user.FirstName} {user.LastName} ({userName}): {user.MessageCount}\n");
        }

        await _telegramBotClient.SendMessage(msg.Chat, replyMessage.ToString(), ParseMode.None,
            new ReplyParameters { MessageId = msg.Id });
    }

    private string GetEmoji(int iterator) =>
        iterator switch
        {
            1 => "🥇",
            2 => "🥈",
            3 => "🥉",
            _ => string.Empty
        };
}
