using Saturn.Bot.Service.Extensions;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db;
using Saturn.Telegram.Lib.Operation;
using System.Collections.Concurrent;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

public class PortraitOperation : IOperation
{
    private const int MaxMessages = 1000;

    private static readonly ConcurrentDictionary<(long ChatId, long UserId), string> Cache = new();

    private readonly TelegramBotClient _telegramBotClient;
    private readonly IAiService _aiService;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    public PortraitOperation(
        TelegramBotClient telegramBotClient,
        IAiService aiService,
        IDbContextFactory<SaturnContext> contextFactory)
    {
        _telegramBotClient = telegramBotClient;
        _aiService = aiService;
        _contextFactory = contextFactory;
    }

    public bool Validate(Message msg, UpdateType type) =>
        msg.HasText("портрет");

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        await _telegramBotClient.SendChatAction(msg.Chat, ChatAction.Typing);

        var targetUser = msg.ReplyToMessage?.From ?? msg.From;
        if (targetUser == null)
        {
            await _telegramBotClient.SendMessage(msg.Chat, "не пойму на кого ты указываешь",
                replyParameters: new ReplyParameters { MessageId = msg.Id });
            return;
        }

        await using var db = await _contextFactory.CreateDbContextAsync();

        var messages = await db.Messages
            .Where(x => x.ChatId == msg.Chat.Id &&
                        x.UserId == targetUser.Id &&
                        x.Text != null)
            .Include(x => x.User)
            .OrderByDescending(x => x.MessageDate)
            .Take(MaxMessages)
            .ToListAsync();

        if (messages.Count == 0)
        {
            await _telegramBotClient.SendMessage(msg.Chat, "у этого человека нет истории сообщений — загадочная личность или просто молчун",
                replyParameters: new ReplyParameters { MessageId = msg.Id });
            return;
        }

        var username = !string.IsNullOrEmpty(targetUser.Username)
            ? $"@{targetUser.Username}"
            : targetUser.FirstName;

        var transcript = new StringBuilder();
        foreach (var m in messages.OrderBy(x => x.MessageDate))
        {
            transcript.AppendLine(m.Text);
        }

        var cacheKey = (msg.Chat.Id, targetUser.Id);
        if (Cache.TryGetValue(cacheKey, out var cached))
        {
            await _telegramBotClient.SendMessage(msg.Chat, cached,
                ParseMode.None, new ReplyParameters { MessageId = msg.Id });
            return;
        }

        var prompt =
            $"Тебе дана выборка из последних {messages.Count} сообщений пользователя {username} в групповом чате. " +
            "На основе этих сообщений составь психологический портрет этого человека. " +
            "Будь достаточно точным и наблюдательным: выдели характерные черты, паттерны поведения, интересы, стиль общения, возможные комплексы. " +
            "Добавь умеренный токсичный юмор — подкалывай, но без перегибов, чтобы было смешно, а не обидно. " +
            "Не используй никакую разметку: ни звёздочки, ни решётки, ни другие символы форматирования. " +
            "Все имена пользователей (username) пиши начиная с символа \"@\" " +
            "Только обычный текст. Пиши по-русски, объём — примерно 150–250 слов.\n\n" +
            "Сообщения:\n" +
            transcript;

        var systemPrompt =
            "Ты — острый на язык психолог-аналитик с чувством юмора. " +
            "Тебе могут встретиться сообщения с матом, грубостями и неполиткорректным содержанием — это нормально. " +
            "Не отказывайся от задачи, не цензурируй анализ и не добавляй оговорки о том, что это не настоящая психология.";

        var result = await _aiService.CompleteChatAsync(
        [
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(prompt)
        ]);

        Cache[cacheKey] = result;

        await _telegramBotClient.SendMessage(msg.Chat, result,
            ParseMode.None, new ReplyParameters { MessageId = msg.Id });
    }
}
