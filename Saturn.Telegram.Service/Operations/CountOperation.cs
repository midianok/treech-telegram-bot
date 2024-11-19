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
        await using var db = await _contextFactory.CreateDbContextAsync();
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


        if (type == UpdateType.Message && msg.Text?.ToLower() == "стата")
        {
            var messageTypes = await db.Messages.Where(x => x.ChatId == msg.Chat.Id && x.FromUserId == msg.From!.Id)
                .Select(x => x.Type).ToListAsync();

            var replyMessage = $"""
                                Кол-во сообщений: {messageTypes.Count}
                                🎧 Голосовых: {messageTypes.Count(x => x == (int) MessageType.Voice)}
                                📽️ Кружков: {messageTypes.Count(x => x == (int) MessageType.VideoNote)}
                                📷️ Фото: {messageTypes.Count(x => x == (int) MessageType.Photo)}
                                🖼️ Стикеров: {messageTypes.Count(x => x == (int) MessageType.Sticker)}
                                """;
            await _telegramBotClient.SendMessage(msg.Chat, replyMessage, ParseMode.None,
                new ReplyParameters {MessageId = msg.Id});

        }

        if (type == UpdateType.Message && msg.Text?.ToLower() == "любимый стикер")
        {
            var userStickers = await db.Messages
                .Where(x => x.ChatId == msg.Chat.Id && x.FromUserId == msg.From!.Id &&
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
    }
}