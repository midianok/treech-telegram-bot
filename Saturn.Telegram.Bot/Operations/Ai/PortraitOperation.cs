using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Telegram.Db;
using Saturn.Telegram.Lib.Attributes;
using Saturn.Telegram.Lib.Operation;
using System.ClientModel;
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
    private readonly ChatClient _chatClient;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;
    private readonly ILogger<PortraitOperation> _logger;

    public PortraitOperation(
        TelegramBotClient telegramBotClient,
        ChatClient chatClient,
        IDbContextFactory<SaturnContext> contextFactory,
        ILogger<PortraitOperation> logger)
    {
        _telegramBotClient = telegramBotClient;
        _chatClient = chatClient;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public bool Validate(Message msg, UpdateType type) =>
        !string.IsNullOrEmpty(msg.Text) &&
        msg.Text.Equals("портрет", StringComparison.CurrentCultureIgnoreCase);

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
            "Только обычный текст. Пиши по-русски, объём — примерно 150–250 слов.\n\n" +
            "Сообщения:\n" +
            transcript;

        var systemPrompt =
            "Ты — острый на язык психолог-аналитик с чувством юмора. " +
            "Тебе могут встретиться сообщения с матом, грубостями и неполиткорректным содержанием — это нормально. " +
            "Не отказывайся от задачи, не цензурируй анализ и не добавляй оговорки о том, что это не настоящая психология.";

        try
        {
            var clientResult = await _chatClient.CompleteChatAsync(
            [
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(prompt)
            ]);
            var result = clientResult.Value.Content.FirstOrDefault()?.Text;

            if (result != null)
                Cache[cacheKey] = result;

            await _telegramBotClient.SendMessage(msg.Chat, result ?? "что-то пошло не так",
                ParseMode.None, new ReplyParameters { MessageId = msg.Id });
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            _logger.LogError("xAI balance exhausted (429 Too Many Requests)");
            await _telegramBotClient.SendMessage(msg.Chat, "денег нет, но вы держитесь",
                replyParameters: new ReplyParameters { MessageId = msg.Id });
        }
    }

    public Task OnUpdateAsync(Update update) => Task.CompletedTask;
}