using System.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Services;

/// <summary>Posts reminders set through the AI chat when their time comes.</summary>
public class ReminderSenderService : BackgroundService
{
    private const int BatchSize = 20;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(20);

    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IReminderRepository _reminderRepository;
    private readonly ILogger<ReminderSenderService> _logger;

    public ReminderSenderService(
        ITelegramBotClient telegramBotClient,
        IReminderRepository reminderRepository,
        ILogger<ReminderSenderService> logger)
    {
        _telegramBotClient = telegramBotClient;
        _reminderRepository = reminderRepository;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                await SendDueRemindersAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to process due reminders");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SendDueRemindersAsync(CancellationToken cancellationToken)
    {
        var reminders = await _reminderRepository.GetDueAsync(DateTime.UtcNow, BatchSize, cancellationToken);
        foreach (var reminder in reminders)
        {
            try
            {
                await _telegramBotClient.SendMessage(
                    reminder.ChatId,
                    BuildText(reminder),
                    ParseMode.Html,
                    reminder.RequestMessageId == null
                        ? null
                        : new ReplyParameters { MessageId = (int)reminder.RequestMessageId, AllowSendingWithoutReply = true },
                    cancellationToken: cancellationToken);
                _logger.LogInformation("Reminder {ReminderId} sent to chat {ChatId} for user {UserId}", reminder.Id, reminder.ChatId, reminder.UserId);
            }
            catch (ApiRequestException ex) when (ex.ErrorCode != (int)HttpStatusCode.TooManyRequests)
            {
                // Telegram refused for good (bot kicked, chat gone): retrying won't help, so drop the reminder
                _logger.LogWarning(ex, "Reminder {ReminderId} for chat {ChatId} rejected by Telegram, dropping it", reminder.Id, reminder.ChatId);
            }

            // Flood limits, network failures and the like throw past this point and leave the reminder pending for the next tick
            await _reminderRepository.MarkSentAsync(reminder.Id, DateTime.UtcNow, cancellationToken);
        }
    }

    private static string BuildText(ReminderEntity reminder)
    {
        var text = $"⏰ {Mention(reminder.User, reminder.UserId)}, напоминаю: {WebUtility.HtmlEncode(reminder.Text)}";
        if (reminder.CreatedByUserId != reminder.UserId)
        {
            text += $"\n<i>просил(а) напомнить {WebUtility.HtmlEncode(FormatName(reminder.CreatedByUser))}</i>";
        }
        return text;
    }

    private static string Mention(UserEntity? user, long userId) =>
        string.IsNullOrEmpty(user?.Username)
            ? $"<a href=\"tg://user?id={userId}\">{WebUtility.HtmlEncode(FormatName(user))}</a>"
            : $"@{user.Username}";

    private static string FormatName(UserEntity? user)
    {
        if (user == null)
        {
            return "кто-то";
        }

        var name = $"{user.FirstName} {user.LastName}".Trim();
        if (!string.IsNullOrEmpty(name))
        {
            return name;
        }
        return string.IsNullOrEmpty(user.Username) ? "кто-то" : $"@{user.Username}";
    }
}
