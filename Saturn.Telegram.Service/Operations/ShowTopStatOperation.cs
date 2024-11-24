using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Saturn.Bot.Service.Database;
using Saturn.Bot.Service.Operations.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations;

public class ShowTopStatOperation : OperationBase
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    public ShowTopStatOperation(TelegramBotClient telegramBotClient, ILogger<ShowTopStatOperation> logger, IDbContextFactory<SaturnContext> contextFactory, IConfiguration configuration) : base(logger, configuration)
    {
        _telegramBotClient = telegramBotClient;
        _contextFactory = contextFactory;
    }

    protected override bool ValidateOnMessage(Message msg, UpdateType type) =>
        type == UpdateType.Message && msg.Text?.ToLower() == "топ стата";

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var db = await _contextFactory.CreateDbContextAsync();

        var monday = GetMondayDate();

        var topUsersByMessageCount = await db.Messages.Where(x => x.ChatId == msg.Chat.Id && x.MessageDate > monday && x.MessageDate < DateTime.Now)
            .Where(x => x.FromUserId.HasValue)
            .GroupBy(x => x.FromUserId)
            .Select(x => new { UserId = x.Key, UserName = x.First().FromUsername, FirstName = x.First().FromFirstName, LastName = x.First().FromLastName, MessageCount = x.Count()})
            .OrderByDescending(x => x.MessageCount)
            .Take(10).ToListAsync();

        var replyMessage = new StringBuilder("Топ за неделю по сообщениям:\n");
        var iterator = 1;

        foreach (var user in topUsersByMessageCount)
        {
            var userName = !string.IsNullOrEmpty(user.UserName) ? user.UserName : user.UserId.ToString();

            var emoji = GetEmoji(iterator++);
            replyMessage.Append($"{emoji} {user.FirstName} {user.LastName} (@{userName}): {user.MessageCount}\n");
        }

        await _telegramBotClient.SendMessage(msg.Chat, replyMessage.ToString(), ParseMode.None,
            new ReplyParameters { MessageId = msg.Id } );
    }


    private DateTime GetMondayDate() =>
        DateTime.Now.DayOfWeek switch
        {
            DayOfWeek.Monday => DateTime.Now.Date,
            DayOfWeek.Tuesday => DateTime.Now.AddDays(-1).Date,
            DayOfWeek.Wednesday => DateTime.Now.AddDays(-2).Date,
            DayOfWeek.Thursday => DateTime.Now.AddDays(-3).Date,
            DayOfWeek.Friday => DateTime.Now.AddDays(-4).Date,
            DayOfWeek.Saturday => DateTime.Now.AddDays(-5).Date,
            DayOfWeek.Sunday => DateTime.Now.AddDays(-6).Date,
            _ => throw new ArgumentOutOfRangeException()
        };

    private string GetEmoji(int iterator) =>
        iterator switch
        {
            1 => "🥇",
            2 => "🥈",
            3 => "🥉",
            _ => string.Empty
        };
}