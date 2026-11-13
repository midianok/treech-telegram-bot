using System.Text.Encodings.Web;
using System.Text.Json;

namespace Saturn.Bot.Service.Services.Tools;

/// <summary>Helpers for reading tool call arguments and writing tool results.</summary>
public static class ToolArguments
{
    // Keep Cyrillic and emoji unescaped so results stay readable and cheap in tokens
    public static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string? GetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = element.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static long? GetLong(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var value)
            ? value
            : null;

    public static bool? GetBool(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var element) && element.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? element.GetBoolean()
            : null;

    public static List<string> GetStrings(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return element.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString()?.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s!)
            .ToList();
    }

    public static string Error(string message) => JsonSerializer.Serialize(new { error = message }, JsonOptions);

    public static string Result(object value) => JsonSerializer.Serialize(value, JsonOptions);

    /// <summary>Drops the emoji variation selector, so "❤️" and "❤" compare equal.</summary>
    public static string NormalizeEmoji(string emoji) => emoji.Replace("️", string.Empty).Trim();
}
