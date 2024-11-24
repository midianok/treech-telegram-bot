using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Saturn.Bot.Service.Database;
using Saturn.Bot.Service.Extension;
using Saturn.Bot.Service.Operations.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using MessageEntity = Saturn.Bot.Service.Database.Entities.MessageEntity;

namespace Saturn.Bot.Service.Operations;

public class CountOperation : OperationBase
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly ILogger<CountOperation> _logger;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    public CountOperation(TelegramBotClient telegramBotClient, ILogger<CountOperation> logger, IDbContextFactory<SaturnContext> contextFactory) : base(logger)
    {
        _telegramBotClient = telegramBotClient;
        _logger = logger;
        _contextFactory = contextFactory;
    }

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var db = await _contextFactory.CreateDbContextAsync();
        await SaveMessage(msg, db);
        await ShowStat(msg, type, db);
        await ShowFavStick(msg, type, db);
        await ShowTop(msg, type, db);
    }

    private async Task ShowTop(Message msg, UpdateType type, SaturnContext db)
    {
        var match = type == UpdateType.Message && msg.Text?.ToLower() == "топ стата";
        if (!match)
        {
            return;
        }

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

    private string GetEmoji(int iterator) =>
        iterator switch
        {
            1 => "🥇",
            2 => "🥈",
            3 => "🥉",
            _ => string.Empty
        };

    private async Task ShowFavStick(Message msg, UpdateType type, SaturnContext db)
    {
        var match = type == UpdateType.Message && msg.Text?.ToLower() == "любимый стикер";
        if (!match)
        {
            return;
        }

        var userId = msg.ReplyToMessage?.From?.Id ?? msg.From!.Id;

        var userStickers = await db.Messages
            .Where(x => x.ChatId == msg.Chat.Id && x.FromUserId == userId &&
                        x.Type == (int) MessageType.Sticker)
            .ToListAsync();

        var favSticker = userStickers.GroupBy(x => x.StickerId)
            .OrderByDescending(grp => grp.Count())
            .FirstOrDefault();

        if (favSticker?.Key == null)
        {
            return;
        }

        await _telegramBotClient.SendSticker(msg.Chat, new InputFileId(favSticker.Key), new ReplyParameters {MessageId = msg.Id});
    }

    private async Task ShowStat(Message msg, UpdateType type, SaturnContext db)
    {
        var match = type == UpdateType.Message && msg.Text?.ToLower() == "стата";
        if (!match)
        {
            return;
        }

        var userId = msg.ReplyToMessage?.From?.Id ?? msg.From!.Id;

        var messageTypes = await db.Messages.Where(x => x.ChatId == msg.Chat.Id && x.FromUserId == userId)
            .Select(x => new { x.Type, x.FromUsername }).ToListAsync();
        var userName = messageTypes.FirstOrDefault()?.FromUsername;

        var replyMessage = $"""
                            Кол-во сообщений пользователя @{ userName ?? userId.ToString() } : {messageTypes.Count} 
                            🎧 Голосовых: {messageTypes.Count(x => x.Type == (int) MessageType.Voice)}
                            📽️ Кружков: {messageTypes.Count(x => x.Type == (int) MessageType.VideoNote)}
                            📷️ Фото: {messageTypes.Count(x => x.Type == (int) MessageType.Photo)}
                            🖼️ Стикеров: {messageTypes.Count(x => x.Type == (int) MessageType.Sticker)}
                            🪄 Гифок: {messageTypes.Count(x => x.Type == (int) MessageType.Animation)}
                            📹 Видео: {messageTypes.Count(x => x.Type == (int) MessageType.Video)}
                            """;

        await _telegramBotClient.SendMessage(msg.Chat, replyMessage, ParseMode.None,
            new ReplyParameters { MessageId = msg.Id } );
    }

    private async Task SaveMessage(Message msg, SaturnContext db)
    {
        await db.Messages.AddAsync(new MessageEntity
        {
            Id = Guid.NewGuid(),
            Type = (int) msg.Type,
            Text = msg.Text,
            MessageDate = msg.Date,
            StickerId = msg.Sticker?.FileId,
            FromUserId = msg.From?.Id,
            FromUsername = msg.From?.Username,
            FromFirstName = msg.From?.FirstName,
            FromLastName = msg.From?.LastName,
            ChatId = msg.Chat.Id,
            ChatType = (int) msg.Chat.Type,
            ChatName = msg.Chat.Username,
            UpdateData = msg.ToJson()
        });
        await db.SaveChangesAsync();
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
}