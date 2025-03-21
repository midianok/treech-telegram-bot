using Microsoft.EntityFrameworkCore;
using Saturn.Bot.Service.Extension;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using MessageEntity = Saturn.Telegram.Db.Entities.MessageEntity;

namespace Saturn.Bot.Service.Operations.Statistics;

public class SaveMessageOperation : OperationBase
{
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    public SaveMessageOperation(IDbContextFactory<SaturnContext> contextFactory) =>
        _contextFactory = contextFactory;

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        if (msg.From == null) return;
    
        var db = await _contextFactory.CreateDbContextAsync();
    
        var user = await db.Users.FindAsync(msg.From.Id);
        if (user == null)
        {
            await db.Users.AddAsync(new UserEntity
            {
                Id = msg.From.Id,
                FirstName = msg.From.FirstName,
                LastName = msg.From.LastName,
                Username = msg.From.Username
            });
        }
    
        var chat = await db.Chats.FindAsync(msg.Chat.Id);
        if (chat == null)
        {
            await db.Chats.AddAsync(new ChatEntity
            {
                Id = msg.Chat.Id,
                Type = (int)msg.Chat.Type,
                Name = msg.Chat.Username
            });
        }
    
        await db.Messages.AddAsync(new MessageEntity
        {
            Id = Guid.NewGuid(),
            ChatId = msg.Chat.Id,
            Type = (int)msg.Type,
            Text = msg.Text,
            MessageDate = msg.Date,
            StickerId = msg.Sticker?.FileId,
            UserId = msg.From.Id,
        });
    
        await db.SaveChangesAsync();
    }
}