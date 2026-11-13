using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Db.Repositories.Abstractions;

namespace Saturn.Bot.Service.Services.Tools;

/// <summary>Schedules a reminder that <see cref="ReminderSenderService"/> posts to the chat later. Created per request.</summary>
public class ReminderChatTool : IChatTool
{
    private const string DefaultTimeZone = ReminderFormatting.DefaultTimeZone;
    private const string DateTimeFormat = ReminderFormatting.DateTimeFormat;
    private const int MaxTextLength = 1000;
    private const int MaxPendingPerUser = 10;
    private const int MaxRemindersPerRequest = 3;
    private static readonly TimeSpan MaxDelay = TimeSpan.FromDays(365);

    private readonly IMessageRepository _messageRepository;
    private readonly IReminderRepository _reminderRepository;
    private readonly ChatToolContext _context;
    private readonly ILogger _logger;
    private int _reminderCount;

    public ReminderChatTool(
        IMessageRepository messageRepository,
        IReminderRepository reminderRepository,
        ChatToolContext context,
        ILogger logger)
    {
        _messageRepository = messageRepository;
        _reminderRepository = reminderRepository;
        _context = context;
        _logger = logger;
    }

    public string FunctionName => "set_reminder";

    public ChatTool Definition { get; } = ChatTool.CreateFunctionTool(
        functionName: "set_reminder",
        functionDescription: "Поставить напоминание: в нужное время бот напишет в этот чат, упомянув человека. " +
                             "Используй, когда просят напомнить («напомни мне через час выключить духовку», " +
                             "«напомни Васе завтра в 10, что он должен 500»). " +
                             "Время задай либо через in_minutes (для «через 2 часа»), либо через at (для «завтра в 10»); " +
                             "для at сначала узнай текущую дату через get_current_datetime. " +
                             "В ответе подтверди, кому и когда напомнишь. " +
                             "Посмотреть и отменить напоминания можно через list_reminders и cancel_reminder.",
        functionParameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                text = new
                {
                    type = "string",
                    description = $"О чём напомнить, коротко, до {MaxTextLength} символов, например «ты должен Пете 500 рублей»"
                },
                user = new
                {
                    type = "string",
                    description = "Кому напомнить: часть username или имени участника чата, как в search_chat_history. " +
                                  "Не указывай, если напомнить нужно самому собеседнику"
                },
                user_id = new
                {
                    type = "integer",
                    description = "Telegram ID участника, если он уже известен (например, из списка кандидатов)"
                },
                in_minutes = new
                {
                    type = "integer",
                    description = "Через сколько минут напомнить"
                },
                at = new
                {
                    type = "string",
                    description = $"Когда напомнить, в формате {DateTimeFormat} по времени time_zone"
                },
                time_zone = new
                {
                    type = "string",
                    description = $"Часовой пояс IANA для at, по умолчанию {DefaultTimeZone}"
                }
            },
            required = new[] { "text" }
        })
    );

    public async Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken)
    {
        if (_reminderCount >= MaxRemindersPerRequest)
        {
            return ToolArguments.Error("Слишком много напоминаний за один раз");
        }

        using var doc = JsonDocument.Parse(arguments);
        var root = doc.RootElement;

        var text = ToolArguments.GetString(root, "text");
        if (text == null || text.Length > MaxTextLength)
        {
            return ToolArguments.Error($"Нужен текст напоминания до {MaxTextLength} символов");
        }

        if (!TryGetRemindAt(root, out var remindAt, out var timeZone, out var timeError))
        {
            return ToolArguments.Error(timeError);
        }

        var request = _context.Request;
        var chatId = request.Chat.Id;
        var createdBy = request.From!.Id;

        var (target, targetError) = await ResolveTargetAsync(root, chatId, createdBy, сancellationToken);
        if (target == null)
        {
            return targetError!;
        }

        if (await _reminderRepository.CountPendingAsync(chatId, createdBy, сancellationToken) >= MaxPendingPerUser)
        {
            return ToolArguments.Error($"У собеседника уже {MaxPendingPerUser} активных напоминаний, больше нельзя");
        }

        await _reminderRepository.AddAsync(new ReminderEntity
        {
            ChatId = chatId,
            UserId = target.UserId,
            CreatedByUserId = createdBy,
            RequestMessageId = request.Id,
            Text = text,
            RemindAt = remindAt,
            CreatedAt = DateTime.UtcNow
        }, сancellationToken);

        _reminderCount++;
        _context.MarkActionTaken();

        var localTime = TimeZoneInfo.ConvertTimeFromUtc(remindAt, timeZone);
        _logger.LogInformation("Reminder set in chat {ChatId} by {CreatedBy} for {Target} at {RemindAt:yyyy-MM-dd HH:mm} UTC: {Text}",
            chatId, createdBy, ChatAuthorMatcher.FormatCandidate(target), remindAt, text);
        return ToolArguments.Result(new
        {
            ok = true,
            remind_at = localTime.ToString(DateTimeFormat, CultureInfo.InvariantCulture),
            time_zone = timeZone.Id,
            user = FormatName(target),
            note = "Напоминание поставлено"
        });
    }

    private static bool TryGetRemindAt(JsonElement root, out DateTime remindAt, out TimeZoneInfo timeZone, out string error)
    {
        remindAt = default;
        error = string.Empty;

        var timeZoneId = ToolArguments.GetString(root, "time_zone");
        if (!ReminderFormatting.TryGetTimeZone(timeZoneId, out timeZone))
        {
            error = $"Неизвестный часовой пояс: {timeZoneId}";
            return false;
        }

        var now = DateTime.UtcNow;
        var inMinutes = ToolArguments.GetLong(root, "in_minutes");
        var at = ToolArguments.GetString(root, "at");
        if (inMinutes != null)
        {
            if (inMinutes < 1 || inMinutes > MaxDelay.TotalMinutes)
            {
                error = "in_minutes должно быть от 1 минуты до года";
                return false;
            }
            remindAt = now.AddMinutes(inMinutes.Value);
        }
        else if (at != null)
        {
            if (!DateTime.TryParseExact(at, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var localTime))
            {
                error = $"at должно быть в формате {DateTimeFormat}";
                return false;
            }
            remindAt = TimeZoneInfo.ConvertTimeToUtc(localTime, timeZone);
        }
        else
        {
            error = "Укажи время: in_minutes или at";
            return false;
        }

        if (remindAt <= now)
        {
            error = "Это время уже прошло — проверь текущую дату через get_current_datetime";
            return false;
        }
        if (remindAt - now > MaxDelay)
        {
            error = "Напоминания ставятся не дальше чем на год вперёд";
            return false;
        }

        return true;
    }

    private async Task<(ChatAuthor? Target, string? Error)> ResolveTargetAsync(
        JsonElement root,
        long chatId,
        long senderId,
        CancellationToken cancellationToken)
    {
        var authors = await _messageRepository.GetChatAuthorsAsync(chatId, cancellationToken);

        var userId = ToolArguments.GetLong(root, "user_id");
        var user = ToolArguments.GetString(root, "user");
        if (userId == null && user == null)
        {
            userId = senderId;
        }

        if (userId != null)
        {
            var byId = authors.FirstOrDefault(a => a.UserId == userId);
            return byId != null ? (byId, null) : (null, ToolArguments.Error("Такого участника нет в чате"));
        }

        var candidates = ChatAuthorMatcher.Find(authors, user!);
        var exact = ChatAuthorMatcher.FindExactUsername(candidates, user!);
        if (exact != null)
        {
            return (exact, null);
        }

        return candidates.Count switch
        {
            1 => (candidates[0], null),
            0 => (null, ToolArguments.Error($"В чате нет участника «{user}». Уточни у собеседника, кому напомнить")),
            _ => (null, ToolArguments.Result(new
            {
                ambiguous_user = true,
                note = "Подходят несколько участников. Спроси собеседника, кого он имел в виду, или повтори с user_id",
                candidates = candidates.Select(a => new { user_id = a.UserId, username = a.Username, name = FormatName(a) })
            }))
        };
    }

    private static string FormatName(ChatAuthor author)
    {
        var name = $"{author.FirstName} {author.LastName}".Trim();
        return string.IsNullOrEmpty(author.Username) ? name : $"{name} (@{author.Username})";
    }
}
