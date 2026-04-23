using Saturn.Bot.Service.Extensions;
using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Lib;
using Saturn.Telegram.Db;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.FunnyStaff;

public class WhoTodayOperation : IOperation
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;
    private readonly ISaveMessageService _saveMessageService;

    public WhoTodayOperation(TelegramBotClient telegramBotClient, IDbContextFactory<SaturnContext> contextFactory, ISaveMessageService saveMessageService)
    {
        _telegramBotClient = telegramBotClient;
        _contextFactory = contextFactory;
        _saveMessageService = saveMessageService;
    }

    public bool Validate(Message msg, UpdateType type) =>
        msg.TextStartsWith("кто сегодня ");

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var randomUser = await db.Messages
            .Where(x => x.ChatId == msg.Chat.Id && x.MessageDate > DateTime.Now.Date)
            .Select(x => x.User!.Username)
            .Distinct()
            .OrderBy(_ => Guid.NewGuid())
            .FirstOrDefaultAsync();

        if (randomUser == null)
        {
            await _telegramBotClient.SendMessage(msg.Chat, "Ты!", ParseMode.None, new ReplyParameters { MessageId = msg.Id });
            return;
        }

        var todayMessage = msg.Text.ToLower().Replace("кто сегодня ", string.Empty);
        var message = await _telegramBotClient.SendMessage(msg.Chat, $"@{randomUser} сегодня {todayMessage}");
        await _saveMessageService.SaveMessageAsync(message);
    }
}
