using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using Saturn.Bot.Service.Extensions;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Lib.Attributes;
using Saturn.Telegram.Lib.Operation;
using System.Collections.Concurrent;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Saturn.Bot.Service.Operations.FunnyStaff;

[ChatOnly]
[Cooldown(300, "подожди немного перед следующей игрой")]
public class GuessWhoOperation : IOperation
{
    private const int MinMessages = 50;
    private const int MessageSampleSize = 200;
    private const int ChoicesCount = 4;
    private static readonly TimeSpan GameDuration = TimeSpan.FromMinutes(30);
    private const string CallbackPrefix = "guesswho:";

    private static readonly ConcurrentDictionary<long, GuessWhoGame> ActiveGames = new();

    private readonly TelegramBotClient _telegramBotClient;
    private readonly IAiService _aiService;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    public GuessWhoOperation(
        TelegramBotClient telegramBotClient,
        IAiService aiService,
        IDbContextFactory<SaturnContext> contextFactory)
    {
        _telegramBotClient = telegramBotClient;
        _aiService = aiService;
        _contextFactory = contextFactory;
    }

    public bool Validate(Message msg, UpdateType type) =>
        msg.HasText("угадай кто");

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        var chatId = msg.Chat.Id;

        if (ActiveGames.TryGetValue(chatId, out var existingGame) && existingGame.ExpiresAt > DateTime.UtcNow)
        {
            await _telegramBotClient.SendMessage(
                msg.Chat,
                "сначала отгадайте текущую загадку!",
                replyParameters: new ReplyParameters { MessageId = msg.Id });
            return;
        }

        await _telegramBotClient.SendChatAction(msg.Chat, ChatAction.Typing);

        await using var db = await _contextFactory.CreateDbContextAsync();

        var eligibleUserIds = await db.Messages
            .Where(x => x.ChatId == chatId && x.Text != null)
            .GroupBy(x => x.UserId)
            .Where(g => g.Count() >= MinMessages)
            .Select(g => g.Key)
            .ToListAsync();

        if (eligibleUserIds.Count < ChoicesCount)
        {
            await _telegramBotClient.SendMessage(
                msg.Chat,
                "недостаточно участников с историей сообщений для игры",
                replyParameters: new ReplyParameters { MessageId = msg.Id });
            return;
        }

        var chosen = eligibleUserIds.OrderBy(_ => Guid.NewGuid()).Take(ChoicesCount).ToList();
        var correctUserId = chosen[0];

        var userEntities = await db.Users
            .Where(x => chosen.Contains(x.Id))
            .ToListAsync();

        var correctUser = userEntities.First(x => x.Id == correctUserId);

        var messages = await db.Messages
            .Where(x => x.ChatId == chatId && x.UserId == correctUserId && x.Text != null)
            .OrderByDescending(x => x.MessageDate)
            .Take(MessageSampleSize)
            .Select(x => x.Text!)
            .ToListAsync();

        messages.Reverse();

        var transcript = new StringBuilder();
        foreach (var m in messages)
            transcript.AppendLine(m);

        var description = await _aiService.CompleteChatAsync(
        [
            new SystemChatMessage(
                "Ты — наблюдательный аналитик общения. " +
                "Описывай стиль человека так, чтобы знакомые по чату могли его узнать. " +
                "Никогда не упоминай имя, username или любые прямые идентификаторы. " +
                "Опиши характерные словечки, темы, манеру письма, юмор, длину сообщений. " +
                "Допустим лёгкий юмор. Только обычный текст без разметки. Пиши по-русски. 100–150 слов."),
            new UserChatMessage(
                $"Вот {messages.Count} сообщений из группового чата:\n\n{transcript}\n\nОпиши этого человека, не называя его.")
        ]);

        var choices = userEntities.OrderBy(_ => Guid.NewGuid()).ToList();
        var keyboard = new InlineKeyboardMarkup(
            choices.Select(u =>
                new[] { InlineKeyboardButton.WithCallbackData(GetDisplayName(u), $"{CallbackPrefix}{u.Id}") }));

        var questionMessage = await _telegramBotClient.SendMessage(
            msg.Chat,
            $"Угадай кто это:\n\n{description}",
            ParseMode.None,
            replyMarkup: keyboard);

        ActiveGames[chatId] = new GuessWhoGame(
            correctUserId,
            GetDisplayName(correctUser),
            questionMessage.Id,
            DateTime.UtcNow.Add(GameDuration));
    }

    public async Task OnUpdateAsync(Update update)
    {
        if (update.CallbackQuery is not { } callbackQuery) return;
        if (callbackQuery.Data is not { } data || !data.StartsWith(CallbackPrefix)) return;
        if (callbackQuery.Message is not { } callbackMsg) return;

        var chatId = callbackMsg.Chat.Id;

        if (!ActiveGames.TryGetValue(chatId, out var game) || game.QuestionMessageId != callbackMsg.Id)
        {
            await _telegramBotClient.AnswerCallbackQuery(callbackQuery.Id, "Игра уже закончилась");
            return;
        }

        if (game.ExpiresAt <= DateTime.UtcNow)
        {
            ActiveGames.TryRemove(chatId, out _);
            await _telegramBotClient.AnswerCallbackQuery(callbackQuery.Id, "Время вышло, игра закончилась");
            return;
        }

        if (!long.TryParse(data[CallbackPrefix.Length..], out var guessedUserId)) return;

        var guesser = callbackQuery.From;
        var guesserName = !string.IsNullOrEmpty(guesser.Username)
            ? $"@{guesser.Username}"
            : guesser.FirstName;

        if (guessedUserId != game.CorrectUserId)
        {
            await _telegramBotClient.AnswerCallbackQuery(callbackQuery.Id, "Мимо! Попробуй ещё раз", showAlert: false);
            return;
        }

        ActiveGames.TryRemove(chatId, out _);
        await _telegramBotClient.AnswerCallbackQuery(callbackQuery.Id, "Правильно!");
        await _telegramBotClient.SendMessage(
            callbackMsg.Chat,
            $"{guesserName} угадал! Это был {game.CorrectUserDisplayName}",
            replyParameters: new ReplyParameters { MessageId = callbackMsg.Id });
    }

    private static string GetDisplayName(UserEntity user) =>
        !string.IsNullOrEmpty(user.Username) ? $"@{user.Username}" : $"{user.FirstName} {user.LastName}".Trim();

    private record GuessWhoGame(
        long CorrectUserId,
        string CorrectUserDisplayName,
        int QuestionMessageId,
        DateTime ExpiresAt);
}
