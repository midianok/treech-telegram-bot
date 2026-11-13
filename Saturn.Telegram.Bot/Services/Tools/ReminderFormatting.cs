using System.Globalization;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Bot.Service.Services.Tools;

/// <summary>Shared bits of the reminder tools: time zones and how reminders are shown to the model.</summary>
public static class ReminderFormatting
{
    public const string DefaultTimeZone = "Europe/Moscow";
    public const string DateTimeFormat = "yyyy-MM-dd HH:mm";

    public static bool TryGetTimeZone(string? timeZoneId, out TimeZoneInfo timeZone)
    {
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId ?? DefaultTimeZone);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            timeZone = TimeZoneInfo.Utc;
            return false;
        }
    }

    public static object ToModel(ReminderEntity reminder, TimeZoneInfo timeZone) => new
    {
        id = reminder.Id,
        remind_at = TimeZoneInfo.ConvertTimeFromUtc(reminder.RemindAt, timeZone).ToString(DateTimeFormat, CultureInfo.InvariantCulture),
        user = FormatName(reminder.User),
        created_by = FormatName(reminder.CreatedByUser),
        text = reminder.Text
    };

    public static string FormatName(UserEntity? user)
    {
        if (user == null)
        {
            return "?";
        }

        var name = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrEmpty(user.Username) ? name : $"{name} (@{user.Username})";
    }
}
