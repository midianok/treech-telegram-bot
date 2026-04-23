using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Saturn.Telegram.Lib;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Entities;
using Telegram.Bot.Types;
using MessageEntity = Saturn.Telegram.Db.Entities.MessageEntity;

namespace Saturn.Bot.Service.Services;

public class SaveMessageService : ISaveMessageService
{
    private readonly IDbContextFactory<SaturnContext> _contextFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<SaveMessageService> _logger;

    public SaveMessageService(IDbContextFactory<SaturnContext> contextFactory, IMemoryCache memoryCache, ILogger<SaveMessageService> logger)
    {
        _contextFactory = contextFactory;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task SaveMessageAsync(Message msg)
    {
        try
        {
            if (msg.From == null) return;

            await using var db = await _contextFactory.CreateDbContextAsync();

            await ProcessUser(msg, db);
            await ProcessChat(msg, db);
            await ProcessMessage(msg, db);

            await db.SaveChangesAsync();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "*Error* saving message: {Message}", exception.Message);
        }
    }
    
    private async Task ProcessMessage(Message msg, SaturnContext db)
    {
        var entity = CreateMessage(msg);
        var exists = await db.Messages.AnyAsync(m => m.Id == msg.Id && m.ChatId == msg.Chat.Id);
        if (exists)
        {
            db.Messages.Update(entity);
        }
        else
        {
            await db.Messages.AddAsync(entity);
        }
    }
    
    private async Task ProcessChat(Message msg, SaturnContext db)
    {
        var chat = await GetCachedEntityById<ChatEntity>(msg.Chat.Id, db, TimeSpan.FromHours(30));;

        if (chat == null)
        {
            await db.Chats.AddAsync(CreateChat(msg));
        }
        else if (chat.Type != (int)msg.Chat.Type || chat.Name != msg.Chat.Username)
        {
            db.Chats.Update(CreateChat(msg));
            RemoveCachedEntityById<ChatEntity>(msg.Chat.Id);
        }
    }

    private async Task ProcessUser(Message msg, SaturnContext db)
    {
        var user = await GetCachedEntityById<UserEntity>(msg.From!.Id, db, TimeSpan.FromMinutes(30));
        if (user == null)
        {
            await db.Users.AddAsync(CreateUser(msg));
        }
        else if (user.FirstName != msg.From.FirstName || user.LastName != msg.From.LastName || user.Username != msg.From.Username)
        {
            db.Users.Update(CreateUser(msg));
            RemoveCachedEntityById<UserEntity>(msg.From!.Id);
        }
    }

    private async Task<T?> GetCachedEntityById<T>(long id, SaturnContext db, TimeSpan expirationTime) where T : class
    {
        var cacheKey = $"{typeof(T).Name}_{id}";
        if (_memoryCache.TryGetValue(cacheKey, out T? cachedEntity))
        {
            return cachedEntity;
        }
        
        var entity = await db.Set<T>().FindAsync(id);
        if (entity == null)
        {
            return null;
        }
        
        _memoryCache.Set(cacheKey, entity, expirationTime);
        return entity;

    }
    
    private void RemoveCachedEntityById<T>(long id) =>
        _memoryCache.Remove($"{typeof(T).Name}_{id}");

    private static MessageEntity CreateMessage(Message msg) =>
        new()
        {
            Id = msg.Id,
            ChatId = msg.Chat.Id,
            Type = (int)msg.Type,
            Text = msg.Text,
            MessageDate = msg.Date,
            StickerId = msg.Sticker?.FileId,
            UserId = msg.From!.Id,
            ReplyToMessageId = msg.ReplyToMessage?.Id,
            ReplyToMessageChatId = msg.ReplyToMessage?.Chat.Id,
        };
    
    private static ChatEntity CreateChat(Message msg) =>
        new()
        {
            Id = msg.Chat.Id,
            Type = (int)msg.Chat.Type,
            Name = msg.Chat.Username
        };

    private static UserEntity CreateUser(Message msg) =>
        new()
        {
            Id = msg.From!.Id,
            FirstName = msg.From.FirstName,
            LastName = msg.From.LastName,
            Username = msg.From.Username
        };
}