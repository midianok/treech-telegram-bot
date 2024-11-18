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
    private readonly SemaphoreSlim _semaphoreSlim = new(1);
    private readonly TelegramBotClient _telegramBotClient;
    private readonly SaturnContext _db;
    private readonly ILogger<CountOperation> _logger;

    public CountOperation(TelegramBotClient telegramBotClient, SaturnContext db, ILogger<CountOperation> logger) : base(logger)
    {
        _telegramBotClient = telegramBotClient;
        _db = db;
        _logger = logger;
    }

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        _db.Messages.Add(new MessageEntity
        {
            Id = Guid.NewGuid(),
            Type = (int) msg.Type,
            Text = msg.Text,
            MessageDate = msg.Date,
            StickerId = msg.Sticker?.FileUniqueId,
            FromUserId = msg.From?.Id,
            FromUsername = msg.From?.Username,
            FromFirstName = msg.From?.FirstName,
            FromLastName = msg.From?.LastName,
            ChatId = msg.Chat.Id,
            ChatType = (int) msg.Chat.Type,
            ChatName = msg.Chat.Username,
            UpdateData = msg.ToJson()
        });
        await _semaphoreSlim.WaitAsync();

        try
        {
            await _db.SaveChangesAsync();
        }
        finally
        {
            _semaphoreSlim.Release();
        }

        if (type == UpdateType.Message && msg.Text == "стата")
        {
            var messageCount = _db.Messages.Count(x => x.ChatId == msg.Chat.Id && x.FromUserId == msg.From!.Id);
            await _telegramBotClient.SendMessage(msg.Chat, $"Кол-во сообщений: {messageCount}", ParseMode.None,
                new ReplyParameters {MessageId = msg.Id});
        }
    }
}