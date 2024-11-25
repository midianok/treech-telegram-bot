using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Saturn.Bot.Service.Database;
using Saturn.Bot.Service.Operations.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations;

public class WhoTodayOperation : OperationBase
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    public WhoTodayOperation(ILogger<IOperation> logger, IConfiguration configuration, TelegramBotClient telegramBotClient, IDbContextFactory<SaturnContext> contextFactory) : base(logger, configuration)
    {
        _telegramBotClient = telegramBotClient;
        _contextFactory = contextFactory;
    }

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var db = await _contextFactory.CreateDbContextAsync();
        var randomUser = await db.Messages
            .Where(x => x.ChatId == msg.Chat.Id && x.MessageDate > DateTime.Now.Date && x.FromUsername != null)
            .Select(x => x.FromUsername)
            .Distinct()
            .OrderBy(_ => Guid.NewGuid())
            .FirstOrDefaultAsync();

        if (randomUser == null)
        {
            await _telegramBotClient.SendMessage(msg.Chat, "Ты!", ParseMode.None, new ReplyParameters { MessageId = msg.Id } );
            return;
        }

        var todayMessage = msg.Text!.Replace("трич кто сегодня ", string.Empty);
        await _telegramBotClient.SendMessage(msg.Chat, $"@{randomUser} сегодня {todayMessage}");
    }

    protected override bool ValidateOnMessage(Message msg, UpdateType type) =>
     type == UpdateType.Message &&
     !string.IsNullOrEmpty(msg.Text) &&
     msg.Text.StartsWith("трич кто сегодня ", StringComparison.CurrentCultureIgnoreCase);
}