using Saturn.Bot.Service.Extensions;
using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Db;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Statistics;

public class ShowFavStickOperation : IOperation
{
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    public ShowFavStickOperation(ITelegramBotClient telegramBotClient, IDbContextFactory<SaturnContext> contextFactory)
    {
        _telegramBotClient = telegramBotClient;
        _contextFactory = contextFactory;
    }

    public bool Validate(Message msg, UpdateType type) =>
        msg.HasText("любимый стикер");

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        var userId = msg.ReplyToMessage?.From?.Id ?? msg.From!.Id;

        await using var db = await _contextFactory.CreateDbContextAsync();
        var favSticker = await db.Messages
            .Where(x => x.ChatId == msg.Chat.Id &&
                        x.UserId == userId &&
                        x.StickerId != null &&
                        x.Type == (int) MessageType.Sticker)
            .GroupBy(x => x.StickerId)
            .OrderByDescending(grp => grp.Count())
            .Select(grp => grp.Key)
            .FirstOrDefaultAsync();

        if (favSticker == null)
        {
            return;
        }

        await _telegramBotClient.SendSticker(msg.Chat, new InputFileId(favSticker), new ReplyParameters { MessageId = msg.Id });
    }
}
