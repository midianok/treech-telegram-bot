using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;

namespace Saturn.Bot.Service.Services.Tools;

public class DateTimeChatTool : IChatTool
{
    private const string DefaultTimeZone = "Europe/Moscow";

    // Explicit names so the result doesn't depend on ICU / culture data in the container
    private static readonly string[] DayNames = ["воскресенье", "понедельник", "вторник", "среда", "четверг", "пятница", "суббота"];
    private static readonly string[] MonthNames = ["январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"];

    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public string FunctionName => "get_current_datetime";

    public ChatTool Definition { get; } = ChatTool.CreateFunctionTool(
        functionName: "get_current_datetime",
        functionDescription: "Получить текущие дату и время: день недели, месяц, номер недели, день года, смещение от UTC. " +
                             "Используй, когда нужно знать, какое сегодня число или сколько сейчас времени, " +
                             "посчитать относительные даты («вчера», «на прошлой неделе») перед поиском по истории чата " +
                             "или сколько осталось до какой-то даты.",
        functionParameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                time_zone = new
                {
                    type = "string",
                    description = $"Часовой пояс IANA, например Europe/Moscow, Asia/Yekaterinburg, UTC. По умолчанию {DefaultTimeZone}"
                }
            }
        })
    );

    public Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken)
    {
        using var doc = JsonDocument.Parse(arguments);
        var timeZoneId = doc.RootElement.TryGetProperty("time_zone", out var timeZoneElement) &&
                         timeZoneElement.ValueKind == JsonValueKind.String &&
                         !string.IsNullOrWhiteSpace(timeZoneElement.GetString())
            ? timeZoneElement.GetString()!.Trim()
            : DefaultTimeZone;

        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return Task.FromResult(JsonSerializer.Serialize(new { error = $"Неизвестный часовой пояс: {timeZoneId}" }, JsonOptions));
        }

        var utcNow = DateTime.UtcNow;
        var now = TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);
        var offset = timeZone.GetUtcOffset(utcNow);

        return Task.FromResult(JsonSerializer.Serialize(new
        {
            time_zone = timeZone.Id,
            utc_offset = $"{(offset < TimeSpan.Zero ? "-" : "+")}{offset:hh\\:mm}",
            date = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            time = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            day_of_week = DayNames[(int)now.DayOfWeek],
            month = MonthNames[now.Month - 1],
            iso_week = ISOWeek.GetWeekOfYear(now),
            day_of_year = now.DayOfYear,
            is_weekend = now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
            utc = utcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        }, JsonOptions));
    }
}
