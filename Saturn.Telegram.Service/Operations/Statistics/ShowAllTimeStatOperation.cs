using System.Text;
using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Db;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Statistics;

public class ShowAllTimeStatOperation : OperationBase
{
    private readonly IDbContextFactory<SaturnContext> _contextFactory;
    
    public ShowAllTimeStatOperation(IDbContextFactory<SaturnContext> contextFactory) =>
        _contextFactory = contextFactory;

    protected override bool ValidateOnTextMessage(Message msg, UpdateType type) =>
        type == UpdateType.Message && msg.Text?.ToLower() == "вся стата";

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var db = await _contextFactory.CreateDbContextAsync();

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

        await TelegramBotClient.SendMessage(msg.Chat, replyMessage.ToString(), ParseMode.None,
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