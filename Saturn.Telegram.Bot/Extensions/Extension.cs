using Microsoft.Extensions.Configuration;
using Telegram.Bot.Types;

namespace Saturn.Bot.Service.Extensions;

public static class Extension
{
    public static string GetSectionOrThrow(this IConfiguration configuration, string key)
    {
        var item = configuration.GetSection(key).Value;
        if (string.IsNullOrWhiteSpace(item))
        {
            throw new Exception($"Configuration item {key} not presented");
        }

        return item;
    }
    
    public static long ParseLong(this string? longString)
    {
        if (long.TryParse(longString, out long result))
        {
            return result;
        }
        throw new Exception($"Unable to parse long string {longString}");
    }

    public static bool HasText(this Message msg, string text) =>
        !string.IsNullOrEmpty(msg.Text) && msg.Text.Equals(text, StringComparison.OrdinalIgnoreCase);

    public static bool TextStartsWith(this Message msg, string prefix) =>
        !string.IsNullOrEmpty(msg.Text) && msg.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

    public static string EscapeMarkdownV2(this string text) => text
        .Replace("_", "\\_")
        .Replace("[", "\\[")
        .Replace("]", "\\]")
        .Replace("(", "\\(")
        .Replace(")", "\\)")
        .Replace("~", "\\~")
        .Replace(">", "\\>")
        .Replace("#", "\\#")
        .Replace("+", "\\+")
        .Replace("-", "\\-")
        .Replace("=", "\\=")
        .Replace("|", "\\|")
        .Replace("{", "\\{")
        .Replace("}", "\\}")
        .Replace(".", "\\.")
        .Replace("!", "\\!");
}