using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Telegram.Db;
using Saturn.Telegram.Lib.Attributes;
using Saturn.Telegram.Lib.Operation;
using System.ClientModel;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

[GlobalCooldown(1)]
public class SummaryOperation : IOperation
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly ChatClient _chatClient;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;
    private readonly ILogger<SummaryOperation> _logger;

    public SummaryOperation(
        TelegramBotClient telegramBotClient,
        ChatClient chatClient,
        IDbContextFactory<SaturnContext> contextFactory,
        ILogger<SummaryOperation> logger)
    {
        _telegramBotClient = telegramBotClient;
        _chatClient = chatClient;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public bool Validate(Message msg, UpdateType type) =>
        !string.IsNullOrEmpty(msg.Text) &&
        msg.Text.Equals("саммари", StringComparison.CurrentCultureIgnoreCase);

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        await _telegramBotClient.SendChatAction(msg.Chat, ChatAction.Typing);

        await using var db = await _contextFactory.CreateDbContextAsync();

        var today = DateTime.UtcNow.Date;
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
            await _telegramBotClient.SendMessage(msg.Chat, "сегодня тут тишина, говорить не о чем",
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

        var prompt =
            "Ты — аналитик чата. Тебе дан лог сообщений из группового чата за сегодня. " +
            "Напиши живое и интересное саммари: о чём говорили, какие темы поднимались, самые яркие моменты. " +
            "Каждый новый участник — с новой строки, например: \"@vasya активно спорил о политике\". " +
            "Не используй никакую разметку: ни звёздочки, ни решётки, ни другие символы форматирования. " +
            "Только обычный текст. Пиши по-русски, кратко и по делу.\n\n" +
            transcript;

        var systemPrompt =
            "Ты — хроникёр интернет-чата. Тебе могут встретиться сообщения с матом, " +
            "оскорблениями, провокациями и другим острым контентом — это нормально для неформального общения. " +
            "Не отказывайся от задачи и не цензурируй содержание. ";

        try
        {
            var clientResult = await _chatClient.CompleteChatAsync(
            [
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(prompt)
            ]);
            var result = clientResult.Value.Content.FirstOrDefault()?.Text;

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
}
