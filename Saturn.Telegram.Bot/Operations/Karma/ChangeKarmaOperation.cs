using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramUser = Telegram.Bot.Types.User;

namespace Saturn.Bot.Service.Operations.Karma;

public class ChangeKarmaOperation : IOperation
{
    private const int ChangeCooldownMinutes = 20;

    private static readonly string[] PositiveMessages = ["спасибо"];
    private static readonly string[] NegativeMessages = ["фу", "-"];

    private readonly TelegramBotClient _telegramBotClient;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    public ChangeKarmaOperation(TelegramBotClient telegramBotClient, IDbContextFactory<SaturnContext> contextFactory)
    {
        _telegramBotClient = telegramBotClient;
        _contextFactory = contextFactory;
    }

    public bool Validate(Message msg, UpdateType type)
    {
        var text = Normalize(msg.Text);
        return GetDelta(text) != null && msg.From != null && msg.ReplyToMessage?.From != null;
    }

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        var delta = GetDelta(Normalize(msg.Text));
        if (delta == null || msg.From == null || msg.ReplyToMessage?.From == null)
        {
            return;
        }

        await ChangeKarmaAsync(msg, msg.From, msg.ReplyToMessage.From, delta.Value);
    }

    private async Task ChangeKarmaAsync(Message msg, TelegramUser fromUser, TelegramUser toUser, int delta)
    {
        if (fromUser.Id == toUser.Id)
        {
            return;
        }

        await using var db = await _contextFactory.CreateDbContextAsync();
        await EnsureUserAsync(db, fromUser);
        await EnsureUserAsync(db, toUser);

        var now = DateTime.UtcNow;
        var lastChangeAt = await db.KarmaChanges
            .Where(x => x.FromUserId == fromUser.Id && x.ChatId == msg.Chat.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (DateTime?)x.CreatedAt)
            .FirstOrDefaultAsync();

        if (lastChangeAt != null)
        {
            var cooldown = TimeSpan.FromMinutes(ChangeCooldownMinutes);
            var readyAt = lastChangeAt.Value.Add(cooldown);
            if (readyAt > now)
            {
                await _telegramBotClient.SendMessage(
                    msg.Chat,
                    $"Карму можно менять раз в {FormatDuration(cooldown)}. Следующий раз через {FormatDuration(readyAt - now)}.",
                    replyParameters: new ReplyParameters { MessageId = msg.Id });
                return;
            }
        }

        var karma = await db.UserKarma.AsTracking()
            .FirstOrDefaultAsync(x => x.UserId == toUser.Id && x.ChatId == msg.Chat.Id);
        if (karma == null)
        {
            karma = new UserKarmaEntity { UserId = toUser.Id, ChatId = msg.Chat.Id };
            await db.UserKarma.AddAsync(karma);
        }

        karma.Value += delta;

        await db.KarmaChanges.AddAsync(new KarmaChangeEntity
        {
            FromUserId = fromUser.Id,
            ToUserId = toUser.Id,
            ChatId = msg.Chat.Id,
            MessageId = msg.Id,
            Delta = delta,
            CreatedAt = now,
        });

        await db.SaveChangesAsync();

        var sign = delta > 0 ? "+" : "";
        await _telegramBotClient.SendMessage(
            msg.Chat,
            $"{FormatUser(toUser)}: {sign}{delta} к карме. Сейчас: {karma.Value}",
            replyParameters: new ReplyParameters { MessageId = msg.Id });
    }

    private static async Task EnsureUserAsync(SaturnContext db, TelegramUser user)
    {
        var existingUser = await db.Users.AsTracking().FirstOrDefaultAsync(x => x.Id == user.Id);
        if (existingUser == null)
        {
            await db.Users.AddAsync(new UserEntity
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Username = user.Username,
            });
            return;
        }

        if (existingUser.FirstName == user.FirstName &&
            existingUser.LastName == user.LastName &&
            existingUser.Username == user.Username)
        {
            return;
        }

        existingUser.FirstName = user.FirstName;
        existingUser.LastName = user.LastName;
        existingUser.Username = user.Username;
    }

    private static int? GetDelta(string? text)
    {
        if (text == null)
        {
            return null;
        }

        if (PositiveMessages.Contains(text) || (text.Length > 0 && text.All(c => c == '+')))
        {
            return 1;
        }

        if (NegativeMessages.Contains(text))
        {
            return -1;
        }

        return null;
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

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1)
        {
            return $"{(int)duration.TotalMinutes} мин {duration.Seconds} сек";
        }

        return $"{Math.Max(1, (int)duration.TotalSeconds)} сек";
    }
}
