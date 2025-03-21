using Microsoft.EntityFrameworkCore;
using Saturn.Bot.Service.Extension;
using Saturn.Telegram.Db;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using MessageEntity = Saturn.Telegram.Db.Entities.MessageEntity;

namespace Saturn.Bot.Service.Operations;

public class SaveMessageOperation : OperationBase
{
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    public SaveMessageOperation(IDbContextFactory<SaturnContext> contextFactory) =>
        _contextFactory = contextFactory;

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var db = await _contextFactory.CreateDbContextAsync();
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
}