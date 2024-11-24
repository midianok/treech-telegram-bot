using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Saturn.Bot.Service.Database;
using Saturn.Bot.Service.Operations.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations;

public class ShowFavStickOperation : OperationBase
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    public ShowFavStickOperation(ILogger<ShowFavStickOperation> logger, TelegramBotClient telegramBotClient, IDbContextFactory<SaturnContext> contextFactory, IConfiguration configuration) : base(logger, configuration)
    {
        _telegramBotClient = telegramBotClient;
        _contextFactory = contextFactory;
    }

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var userId = msg.ReplyToMessage?.From?.Id ?? msg.From!.Id;

        var db = await _contextFactory.CreateDbContextAsync();
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

    protected override bool ValidateOnMessage(Message msg, UpdateType type) =>
        type == UpdateType.Message && msg.Text?.ToLower() == "любимый стикер";
}