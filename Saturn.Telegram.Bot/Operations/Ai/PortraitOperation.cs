using Saturn.Bot.Service.Extensions;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db;
using Saturn.Telegram.Lib.Operation;
using System.Collections.Concurrent;
using System.Text;
using Saturn.Telegram.Lib.Extensions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Saturn.Bot.Service.Services.Prompts;

namespace Saturn.Bot.Service.Operations.Ai;

public class PortraitOperation : IOperation
{
    private const int MaxMessages = 300;

    private static readonly ConcurrentDictionary<(long ChatId, long UserId), string> Cache = new();

    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IAiService _aiService;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    public PortraitOperation(
        ITelegramBotClient telegramBotClient,
        IAiService aiService,
        IDbContextFactory<SaturnContext> contextFactory)
    {
        _telegramBotClient = telegramBotClient;
        _aiService = aiService;
        _contextFactory = contextFactory;
    }

    public bool Validate(Message msg, UpdateType type) =>
        msg.HasText("портрет");

    public async Task OnMessageAsync(Message msg, UpdateType type, CancellationToken сancellationToken)
    {
        await _telegramBotClient.SendChatAction(msg.Chat, ChatAction.Typing, cancellationToken: сancellationToken);

        var targetUser = msg.ReplyToMessage?.From ?? msg.From;
        if (targetUser == null)
        {
            await _telegramBotClient.SendMessage(msg.Chat, "не пойму на кого ты указываешь",
                replyParameters: new ReplyParameters { MessageId = msg.Id }, cancellationToken: сancellationToken);
            return;
        }

        await using var db = await _contextFactory.CreateDbContextAsync(сancellationToken);

        var messages = await db.Messages
            .Where(x => x.ChatId == msg.Chat.Id &&
                        x.UserId == targetUser.Id &&
                        x.Text != null)
            .Include(x => x.User)
            .OrderByDescending(x => x.MessageDate)
            .Take(MaxMessages)
            .ToListAsync(сancellationToken);

        if (messages.Count == 0)
        {
            await _telegramBotClient.SendMessage(msg.Chat, "у этого человека нет истории сообщений — загадочная личность или просто молчун",
                replyParameters: new ReplyParameters { MessageId = msg.Id }, cancellationToken: сancellationToken);
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
                ParseMode.None, new ReplyParameters { MessageId = msg.Id }, cancellationToken: сancellationToken);
            return;
        }

        var prompt = PromptLibrary.Get("Portrait/Request", new { count = messages.Count, username, transcript });
        var systemPrompt = PromptLibrary.Get("Portrait/System");

        var result = await _telegramBotClient.WithChatActionAsync(msg.Chat, ChatAction.Typing, () => _aiService.CompleteChatAsync(
        [
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(prompt)
        ], сancellationToken), сancellationToken);

        Cache[cacheKey] = result;

        await _telegramBotClient.SendMessage(msg.Chat, result,
            ParseMode.None, new ReplyParameters { MessageId = msg.Id }, cancellationToken: сancellationToken);
    }
}
