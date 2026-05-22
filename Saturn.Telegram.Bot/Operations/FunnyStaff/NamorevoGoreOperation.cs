using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Saturn.Bot.Service.Extensions;
using Saturn.Bot.Service.Options;
using Saturn.Telegram.Db;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Saturn.Bot.Service.Operations.FunnyStaff;

public class NamorevoGoreOperation(
    TelegramBotClient telegramBotClient,
    IDbContextFactory<SaturnContext> contextFactory,
    IOptions<BotOptions> botOptions) : IOperation
{
    public bool Validate(Message msg, UpdateType type) =>
        !string.IsNullOrEmpty(msg.Text) &&
        msg.HasText("наморево горе") || msg.HasText("наморово горе");

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        await using var db = await contextFactory.CreateDbContextAsync();

        var top = await db.NamorevoGoreScores
            .Include(x => x.User)
            .Where(x => x.ChatId == msg.Chat.Id)
            .OrderByDescending(x => x.Score)
            .Take(10)
            .ToListAsync();

        var replyMessage = new StringBuilder("Наморево горе!\n\nТоп игроков:\n");
        var iterator = 1;

        foreach (var entry in top)
        {
            var userName = FormatUser(entry.UserId, entry.User?.Username, entry.User?.FirstName, entry.User?.LastName);
            var emoji = GetEmoji(iterator++);
            replyMessage.Append($"{emoji} {userName}: {entry.Score}\n");
        }

        var keyboard = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithUrl("Наморево горе", $"https://t.me/{botOptions.Value.BotUsername}/namorevogore?startapp={msg.Chat.Id}"));

        var imagePath = Path.Combine(AppContext.BaseDirectory, "Media", "namorevo.jpg");
        await using var stream = File.OpenRead(imagePath);
        var inputFile = new InputFileStream(stream, "namorevo.jpg");

        await telegramBotClient.SendPhoto(
            msg.Chat,
            inputFile,
            caption: replyMessage.ToString(),
            replyParameters: new ReplyParameters { MessageId = msg.Id },
            replyMarkup: keyboard);
    }

    private static string FormatUser(long userId, string? username, string? firstName, string? lastName)
    {
        if (!string.IsNullOrWhiteSpace(username))
        {
            return $"{username}";
        }

        var fullName = string.Join(' ', new[] { firstName, lastName }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        return string.IsNullOrWhiteSpace(fullName) ? userId.ToString() : fullName;
    }

    private static string GetEmoji(int position) =>
        position switch
        {
            1 => "🥇",
            2 => "🥈",
            3 => "🥉",
            _ => string.Empty
        };
}
