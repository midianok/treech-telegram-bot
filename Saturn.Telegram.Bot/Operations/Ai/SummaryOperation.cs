using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db;
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
    private readonly TelegramBotClient _telegramBotClient;
    private readonly IAiService _aiService;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    public SummaryOperation(
        TelegramBotClient telegramBotClient,
        IAiService aiService,
        IDbContextFactory<SaturnContext> contextFactory)
    {
        _telegramBotClient = telegramBotClient;
        _aiService = aiService;
        _contextFactory = contextFactory;
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

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        await _telegramBotClient.SendChatAction(msg.Chat, ChatAction.Typing);

        await using var db = await _contextFactory.CreateDbContextAsync();

        var parts = msg.Text!.Trim().Split(' ', 2);
        DateTime today;
        if (parts.Length == 2 && DateOnly.TryParseExact(parts[1].Trim(), "yyyy-MM-dd", out var parsedDate))
            today = parsedDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        else
            today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var messages = await db.Messages
            .Where(x => x.ChatId == msg.Chat.Id &&
                        x.UserId != 5990847351 &&
                        x.MessageDate >= today &&
                        x.MessageDate < tomorrow &&
                        x.Text != null)
            .Include(x => x.User)
            .OrderBy(x => x.MessageDate)
            .ToListAsync();

        if (messages.Count == 0)
        {
            var dateLabel = parts.Length == 2 ? parts[1].Trim() : "сегодня";
            await _telegramBotClient.SendMessage(msg.Chat, $"{dateLabel} тут тишина, говорить не о чем",
                replyParameters: new ReplyParameters { MessageId = msg.Id });
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
            $"Ты — аналитик чата. Тебе дан лог сообщений из группового чата за {dayLabel}. " +
            "Напиши живое и интересное саммари: о чём говорили, какие темы поднимались, самые яркие моменты. " +
            "Каждый новый участник — с новой строки, например: \"@vasya активно спорил о политике\". " +
            "Не используй никакую разметку: ни звёздочки, ни решётки, ни другие символы форматирования. " +
            "Только обычный текст. Пиши по-русски, кратко и по делу.\n\n" +
            transcript;

        var systemPrompt =
            "Ты — хроникёр интернет-чата. Тебе могут встретиться сообщения с матом, " +
            "оскорблениями, провокациями и другим острым контентом — это нормально для неформального общения. " +
            "Не отказывайся от задачи и не цензурируй содержание. ";

        var result = await _aiService.CompleteChatAsync(
        [
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(prompt)
        ]);

        await _telegramBotClient.SendMessage(msg.Chat, result,
            ParseMode.None, new ReplyParameters { MessageId = msg.Id });
    }
}
