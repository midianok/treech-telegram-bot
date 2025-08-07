using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Db;
using Saturn.Telegram.Lib.Operation;
using Saturn.Telegram.Lib.Services.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.FunnyStaff;

public class WhoTodayOperation : OperationBase
{
    private readonly IDbContextFactory<SaturnContext> _contextFactory;
    private readonly ISaveMessageService _saveMessageService;

    public WhoTodayOperation(IDbContextFactory<SaturnContext> contextFactory, ISaveMessageService saveMessageService)
    {
        _contextFactory = contextFactory;
        _saveMessageService = saveMessageService;
    }

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var db = await _contextFactory.CreateDbContextAsync();
        var randomUser = await db.Messages
            .Where(x => x.ChatId == msg.Chat.Id && x.MessageDate > DateTime.Now.Date)
            .Select(x => x.User!.Username)
            .Distinct()
            .OrderBy(_ => Guid.NewGuid())
            .FirstOrDefaultAsync();

        if (randomUser == null)
        {
            await TelegramBotClient.SendMessage(msg.Chat, "Ты!", ParseMode.None, new ReplyParameters { MessageId = msg.Id } );
            return;
        }

        var todayMessage = msg.Text!.ToLower().Replace("кто сегодня ", string.Empty);
        var message = await TelegramBotClient.SendMessage(msg.Chat, $"@{randomUser} сегодня {todayMessage}");
        await _saveMessageService.SaveMessageAsync(message);
    }

    protected override bool ValidateMessage(Message msg, UpdateType type) =>
        !string.IsNullOrEmpty(msg.Text) && msg.Text.StartsWith("кто сегодня ", StringComparison.CurrentCultureIgnoreCase);
}