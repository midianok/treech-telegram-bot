using System.Text.Json;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db.Repositories.Abstractions;

namespace Saturn.Bot.Service.Services.Tools;

/// <summary>Lists the pending reminders the requester set or is reminded by in this chat. Created per request.</summary>
public class ListRemindersChatTool : IChatTool
{
    private readonly IReminderRepository _reminderRepository;
    private readonly ChatToolContext _context;

    public ListRemindersChatTool(IReminderRepository reminderRepository, ChatToolContext context)
    {
        _reminderRepository = reminderRepository;
        _context = context;
    }

    public string FunctionName => "list_reminders";

    public ChatTool Definition { get; } = ChatTool.CreateFunctionTool(
        functionName: "list_reminders",
        functionDescription: "Показать активные напоминания собеседника в этом чате: те, что он поставил сам, и те, что поставили ему. " +
                             "Используй, когда спрашивают «какие у меня напоминания?», и перед cancel_reminder, чтобы узнать id.",
        functionParameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                time_zone = new
                {
                    type = "string",
                    description = $"Часовой пояс IANA для времени напоминаний, по умолчанию {ReminderFormatting.DefaultTimeZone}"
                }
            }
        })
    );

    public async Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken)
    {
        using var doc = JsonDocument.Parse(arguments);
        var timeZoneId = ToolArguments.GetString(doc.RootElement, "time_zone");
        if (!ReminderFormatting.TryGetTimeZone(timeZoneId, out var timeZone))
        {
            return ToolArguments.Error($"Неизвестный часовой пояс: {timeZoneId}");
        }

        var request = _context.Request;
        var reminders = await _reminderRepository.GetPendingForUserAsync(request.Chat.Id, request.From!.Id, сancellationToken);
        if (reminders.Count == 0)
        {
            return ToolArguments.Result(new { reminders = Array.Empty<object>(), note = "Активных напоминаний нет" });
        }

        return ToolArguments.Result(new
        {
            time_zone = timeZone.Id,
            note = "user — кому напомнят, created_by — кто поставил",
            reminders = reminders.Select(r => ReminderFormatting.ToModel(r, timeZone))
        });
    }
}
