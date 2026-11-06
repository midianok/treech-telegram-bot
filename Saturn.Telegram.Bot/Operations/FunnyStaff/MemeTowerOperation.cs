using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Saturn.Bot.Service.Extensions;
using Saturn.Bot.Service.Options;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Saturn.Bot.Service.Operations.FunnyStaff;

/// <summary>
/// «мемный трич» — топ игроков чата и кнопка запуска Mini App с башней из мемов.
/// </summary>
public class MemeTowerOperation(
    ITelegramBotClient telegramBotClient,
    IDbContextFactory<SaturnContext> contextFactory,
    IOptions<BotOptions> botOptions) : IOperation
{
    private const string MiniAppShortName = "memnytrich";
    private const string ImageFileName = "memnytreech.png";

    public bool Validate(Message msg, UpdateType type) => msg.HasText("мемный трич");

    public async Task OnMessageAsync(Message msg, UpdateType type, CancellationToken сancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(сancellationToken);

        var top = await db.MemeTowerScores
            .Include(x => x.User)
            .Where(x => x.ChatId == msg.Chat.Id)
            .OrderByDescending(x => x.Score)
            .Take(10)
            .ToListAsync(сancellationToken);

        var replyMessage = new StringBuilder("Мемный трич!\n\n");
        if (top.Count == 0)
        {
            replyMessage.Append("Пока никто не построил башню. Будь первым!\n");
        }
        else
        {
            replyMessage.Append("Топ строителей:\n");
            replyMessage.Append(GameLeaderboardFormatter.Format(top));
        }

        var keyboard = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithUrl("Мемный трич", $"https://t.me/{botOptions.Value.BotUsername}/{MiniAppShortName}?startapp={msg.Chat.Id}"));

        var imagePath = Path.Combine(AppContext.BaseDirectory, "Media", ImageFileName);
        await using var stream = File.OpenRead(imagePath);
        var inputFile = new InputFileStream(stream, ImageFileName);

        await telegramBotClient.SendPhoto(
            msg.Chat,
            inputFile,
            caption: replyMessage.ToString(),
            parseMode: ParseMode.Html,
            replyParameters: new ReplyParameters { MessageId = msg.Id },
            replyMarkup: keyboard,
            cancellationToken: сancellationToken);
    }
}
