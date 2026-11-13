using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db.Repositories.Abstractions;

namespace Saturn.Bot.Service.Services.Tools;

/// <summary>Cancels a pending reminder the requester set or is reminded by. Created per request.</summary>
public class CancelReminderChatTool : IChatTool
{
    private readonly IReminderRepository _reminderRepository;
    private readonly ChatToolContext _context;
    private readonly bool _isAdmin;
    private readonly ILogger _logger;

    public CancelReminderChatTool(IReminderRepository reminderRepository, ChatToolContext context, bool isAdmin, ILogger logger)
    {
        _reminderRepository = reminderRepository;
        _context = context;
        _isAdmin = isAdmin;
        _logger = logger;
    }

    public string FunctionName => "cancel_reminder";

    public ChatTool Definition { get; } = ChatTool.CreateFunctionTool(
        functionName: "cancel_reminder",
        functionDescription: "Отменить напоминание по id. Отменить можно напоминание, которое собеседник поставил сам или которое поставили ему. " +
                             "id бери из list_reminders, не угадывай; если под просьбу подходят несколько напоминаний — " +
                             "уточни у собеседника, какое отменить, либо отмени все, если он просит все. В ответе подтверди, что отменил.",
        functionParameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                reminder_id = new
                {
                    type = "integer",
                    description = "id напоминания из list_reminders"
                }
            },
            required = new[] { "reminder_id" }
        })
    );

    public async Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken)
    {
        using var doc = JsonDocument.Parse(arguments);
        var reminderId = ToolArguments.GetLong(doc.RootElement, "reminder_id");
        if (reminderId == null)
        {
            return ToolArguments.Error("Укажи reminder_id из list_reminders");
        }

        var request = _context.Request;
        var reminder = await _reminderRepository.GetPendingAsync(request.Chat.Id, reminderId.Value, сancellationToken);
        if (reminder == null)
        {
            return ToolArguments.Error("Такого активного напоминания нет — возможно, оно уже сработало. Проверь list_reminders");
        }

        var userId = request.From!.Id;
        if (!_isAdmin && reminder.CreatedByUserId != userId && reminder.UserId != userId)
        {
            return ToolArguments.Error("Это чужое напоминание: отменить его может только тот, кто поставил, или тот, кому напомнят");
        }

        await _reminderRepository.DeleteAsync(reminder.Id, сancellationToken);
        _logger.LogInformation("Reminder {ReminderId} in chat {ChatId} cancelled by {UserId}: {Text}", reminder.Id, reminder.ChatId, userId, reminder.Text);

        ReminderFormatting.TryGetTimeZone(null, out var timeZone);
        return ToolArguments.Result(new
        {
            ok = true,
            cancelled = ReminderFormatting.ToModel(reminder, timeZone),
            time_zone = timeZone.Id
        });
    }
}
