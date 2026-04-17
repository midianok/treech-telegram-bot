using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Db;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Statistics;

public class ShowFavStickOperation : IOperation
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    public ShowFavStickOperation(TelegramBotClient telegramBotClient, IDbContextFactory<SaturnContext> contextFactory)
    {
        _telegramBotClient = telegramBotClient;
        _contextFactory = contextFactory;
    }

    public bool Validate(Message msg, UpdateType type) =>
        !string.IsNullOrEmpty(msg.Text) && msg.Text.Equals("любимый стикер", StringComparison.CurrentCultureIgnoreCase);

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        var userId = msg.ReplyToMessage?.From?.Id ?? msg.From!.Id;

        await using var db = await _contextFactory.CreateDbContextAsync();
        var userStickers = await db.Messages
            .Where(x => x.ChatId == msg.Chat.Id && x.UserId == userId &&
                        x.Type == (int) MessageType.Sticker)
            .ToListAsync();

        var favSticker = userStickers.GroupBy(x => x.StickerId)
            .OrderByDescending(grp => grp.Count())
            .FirstOrDefault();

        if (favSticker?.Key == null)
        {
            return;
        }

        await _telegramBotClient.SendSticker(msg.Chat, new InputFileId(favSticker.Key), new ReplyParameters { MessageId = msg.Id });
    }
}
