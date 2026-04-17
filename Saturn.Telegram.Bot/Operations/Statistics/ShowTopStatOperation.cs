using System.Text;
using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Db;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Saturn.Bot.Service.Operations.Statistics;

public class ShowTopStatOperation : IOperation
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    public ShowTopStatOperation(TelegramBotClient telegramBotClient, IDbContextFactory<SaturnContext> contextFactory)
    {
        _telegramBotClient = telegramBotClient;
        _contextFactory = contextFactory;
    }

    public bool Validate(Message msg, UpdateType type) =>
        !string.IsNullOrEmpty(msg.Text) && msg.Text.Equals("топ стата", StringComparison.CurrentCultureIgnoreCase);

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var monday = GetMondayDate();

        var topUsersByMessageCount = await db.Messages
            .Where(x => x.ChatId == msg.Chat.Id && x.MessageDate >= monday && x.MessageDate < DateTime.Now)
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

        var replyMessage = new StringBuilder("Топ за неделю по сообщениям:\n");
        var iterator = 1;

        foreach (var user in topUsersByMessageCount)
        {
            var userName = !string.IsNullOrEmpty(user.Username) ? $"@{user.Username}" : user.UserId.ToString();
            var emoji = GetEmoji(iterator++);
            replyMessage.Append($"{emoji} {user.FirstName} {user.LastName} ({userName}): {user.MessageCount}\n");
        }

        var keyboard = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithUrl("Открыть приложение", $"https://t.me/TreechBot/app?startapp={msg.Chat.Id}"));

        await _telegramBotClient.SendMessage(msg.Chat, replyMessage.ToString(), ParseMode.None,
            new ReplyParameters { MessageId = msg.Id }, replyMarkup: keyboard);
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
