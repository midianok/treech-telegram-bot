using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Db;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Saturn.Bot.Service.Operations.Statistics;

public class ShowUserStatOperation : IOperation
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    public ShowUserStatOperation(TelegramBotClient telegramBotClient, IDbContextFactory<SaturnContext> contextFactory)
    {
        _telegramBotClient = telegramBotClient;
        _contextFactory = contextFactory;
    }

    public bool Validate(Message msg, UpdateType type) =>
        !string.IsNullOrEmpty(msg.Text) && msg.Text.Equals("стата", StringComparison.CurrentCultureIgnoreCase);

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();
        var userId = msg.ReplyToMessage?.From?.Id ?? msg.From!.Id;

        var messageTypes = await db.Messages.Where(x => x.ChatId == msg.Chat.Id && x.UserId == userId)
            .Select(x => new { x.Type, x.User!.Username })
            .ToListAsync();

        var userName = messageTypes.FirstOrDefault()?.Username;

        var replyMessage = $"""
                            Кол-во сообщений пользователя @{ userName ?? userId.ToString() } : {messageTypes.Count}
                            🎧 Голосовых: {messageTypes.Count(x => x.Type == (int) MessageType.Voice)}
                            📽️ Кружков: {messageTypes.Count(x => x.Type == (int) MessageType.VideoNote)}
                            📷️ Фото: {messageTypes.Count(x => x.Type == (int) MessageType.Photo)}
                            🖼️ Стикеров: {messageTypes.Count(x => x.Type == (int) MessageType.Sticker)}
                            🪄 Гифок: {messageTypes.Count(x => x.Type == (int) MessageType.Animation)}
                            📹 Видео: {messageTypes.Count(x => x.Type == (int) MessageType.Video)}
                            """;

        var keyboard = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithUrl("Открыть приложение", $"https://t.me/TreechBot/app?startapp={msg.Chat.Id}"));

        await _telegramBotClient.SendMessage(msg.Chat, replyMessage, ParseMode.None,
            new ReplyParameters { MessageId = msg.Id }, replyMarkup: keyboard);
    }

    public Task OnUpdateAsync(Update update) => Task.CompletedTask;
}
