using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Db;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations;

public class ShowUserStatOperation : OperationBase
{
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    public ShowUserStatOperation(IDbContextFactory<SaturnContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var db = await _contextFactory.CreateDbContextAsync();
        var userId = msg.ReplyToMessage?.From?.Id ?? msg.From!.Id;

        var messageTypes = await db.Messages.Where(x => x.ChatId == msg.Chat.Id && x.FromUserId == userId)
            .Select(x => new { x.Type, x.FromUsername })
            .ToListAsync();

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

        await TelegramBotClient.SendMessage(msg.Chat, replyMessage, ParseMode.None, new ReplyParameters { MessageId = msg.Id } );
    }

    protected override bool ValidateOnMessage(Message msg, UpdateType type) =>
        type == UpdateType.Message && msg.Text?.ToLower() == "стата";
}