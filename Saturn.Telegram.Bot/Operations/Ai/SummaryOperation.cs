using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Saturn.Telegram.Lib.Attributes;
using Saturn.Telegram.Lib.Operation;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

[GlobalCooldown(1)]
public class SummaryOperation : IOperation
{
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IAiService _aiService;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;
    private readonly IChatCachedRepository _chatCachedRepository;

    public SummaryOperation(
        ITelegramBotClient telegramBotClient,
        IAiService aiService,
        IDbContextFactory<SaturnContext> contextFactory,
        IChatCachedRepository chatCachedRepository)
    {
        _telegramBotClient = telegramBotClient;
        _aiService = aiService;
        _contextFactory = contextFactory;
        _chatCachedRepository = chatCachedRepository;
    }

    public bool Validate(Message msg, UpdateType type)
    {
        if (string.IsNullOrEmpty(msg.Text))
            return false;
        var parts = msg.Text.Trim().Split(' ', 2);
        if (!parts[0].Equals("саммари", StringComparison.CurrentCultureIgnoreCase))
            return false;
        if (parts.Length == 1)
            return true;
        return DateOnly.TryParseExact(parts[1].Trim(), "yyyy-MM-dd", out _);
    }

    public async Task OnMessageAsync(Message msg, UpdateType type, CancellationToken сancellationToken)
    {
        await _telegramBotClient.SendChatAction(msg.Chat, ChatAction.Typing, cancellationToken: сancellationToken);

        await using var db = await _contextFactory.CreateDbContextAsync(сancellationToken);

        var parts = msg.Text!.Trim().Split(' ', 2);
        DateTime today;
        if (parts.Length == 2 && DateOnly.TryParseExact(parts[1].Trim(), "yyyy-MM-dd", out var parsedDate))
            today = parsedDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        else
            today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var messages = await db.Messages
            .Where(x => x.ChatId == msg.Chat.Id &&
                        !x.IsBot &&
                        x.MessageDate >= today &&
                        x.MessageDate < tomorrow &&
                        x.Text != null)
            .Include(x => x.User)
            .OrderBy(x => x.MessageDate)
            .ToListAsync(сancellationToken);

        if (messages.Count == 0)
        {
            var dateLabel = parts.Length == 2 ? parts[1].Trim() : "сегодня";
            await _telegramBotClient.SendMessage(msg.Chat, $"{dateLabel} тут тишина, говорить не о чем",
                replyParameters: new ReplyParameters { MessageId = msg.Id }, cancellationToken: сancellationToken);
            return;
        }

        var transcript = new StringBuilder();
        foreach (var m in messages)
        {
            var username = !string.IsNullOrEmpty(m.User?.Username)
                ? $"@{m.User.Username}"
                : m.User?.FirstName ?? m.UserId.ToString();
            transcript.AppendLine($"{username}: {m.Text}");
        }

        var dayLabel = parts.Length == 2 ? parts[1].Trim() : "сегодня";
        var prompt =
            $"Ниже лог сообщений из группового чата за {dayLabel}. " +
            "Сделай саммари: о чём говорили, какие темы поднимались, самые яркие моменты. " +
            "Каждый новый участник — с новой строки, например: \"@vasya активно спорил о политике\". " +
            "Не используй никакую разметку: ни звёздочки, ни решётки, ни другие символы форматирования. " +
            "Только обычный текст. Пиши по-русски, кратко и по делу. " +
            "В логе могут встретиться мат, оскорбления, провокации и другой острый контент — " +
            "это нормально для неформального общения, не отказывайся от задачи и не цензурируй содержание.\n\n" +
            transcript;

        var chatMessages = new List<ChatMessage>();

        var chatEntity = await _chatCachedRepository.GetAsync(msg.Chat.Id);
        if (!string.IsNullOrEmpty(chatEntity.AiAgent?.Prompt))
        {
            chatMessages.Add(new SystemChatMessage(chatEntity.AiAgent.Prompt));
        }

        chatMessages.Add(new UserChatMessage(prompt));

        var result = await _aiService.CompleteChatAsync(chatMessages, сancellationToken);

        await _telegramBotClient.SendMessage(msg.Chat, result,
            ParseMode.None, new ReplyParameters { MessageId = msg.Id }, cancellationToken: сancellationToken);
    }
}
