using System.Text.RegularExpressions;

namespace Saturn.Bot.Service.Extensions;

public static partial class MarkdownExtensions
{
    /// <summary>
    /// Turns a markdown answer into plain text: removes emphasis, headers, code fences and quotes,
    /// keeps link targets. Single underscores are left alone so usernames like @vasya_pupkin survive.
    /// </summary>
    public static string StripMarkdown(this string text)
    {
        text = CodeBlockRegex().Replace(text, "$1");
        text = InlineCodeRegex().Replace(text, "$1");
        text = HeaderRegex().Replace(text, string.Empty);
        text = QuoteRegex().Replace(text, string.Empty);
        text = HorizontalRuleRegex().Replace(text, string.Empty);
        text = TableSeparatorRegex().Replace(text, string.Empty);
        text = BulletRegex().Replace(text, "$1- ");
        text = LinkRegex().Replace(text, m => m.Groups[1].Value == m.Groups[2].Value
            ? m.Groups[2].Value
            : $"{m.Groups[1].Value} ({m.Groups[2].Value})");
        text = BoldAsteriskRegex().Replace(text, "$1");
        text = BoldUnderscoreRegex().Replace(text, "$1");
        text = StrikethroughRegex().Replace(text, "$1");
        text = ItalicAsteriskRegex().Replace(text, "$1");
        text = ExtraNewLinesRegex().Replace(text, "\n\n");
        return text.Trim();
    }

    [GeneratedRegex(@"```[^\n`]*\n?([\s\S]*?)```")]
    private static partial Regex CodeBlockRegex();

    [GeneratedRegex(@"`([^`\n]+)`")]
    private static partial Regex InlineCodeRegex();

    [GeneratedRegex(@"^[ \t]*#{1,6}[ \t]+", RegexOptions.Multiline)]
    private static partial Regex HeaderRegex();

    [GeneratedRegex(@"^[ \t]*>[ \t]?", RegexOptions.Multiline)]
    private static partial Regex QuoteRegex();

    [GeneratedRegex(@"^[ \t]*([-*_])([ \t]*\1){2,}[ \t]*$\n?", RegexOptions.Multiline)]
    private static partial Regex HorizontalRuleRegex();

    [GeneratedRegex(@"^[ \t]*\|?[ \t]*:?-{3,}:?[ \t]*(\|[ \t]*:?-{3,}:?[ \t]*)+\|?[ \t]*$\n?", RegexOptions.Multiline)]
    private static partial Regex TableSeparatorRegex();

    [GeneratedRegex(@"^([ \t]*)[*+][ \t]+", RegexOptions.Multiline)]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"\[([^\]\n]+)\]\(([^)\s]+)\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"\*\*(?!\s)(.+?)(?<!\s)\*\*")]
    private static partial Regex BoldAsteriskRegex();

    [GeneratedRegex(@"(?<!\w)__(?!\s)(.+?)(?<!\s)__(?!\w)")]
    private static partial Regex BoldUnderscoreRegex();

    [GeneratedRegex(@"~~(?!\s)(.+?)(?<!\s)~~")]
    private static partial Regex StrikethroughRegex();

    [GeneratedRegex(@"(?<![\w*])\*(?![\s*])(.+?)(?<![\s*])\*(?![\w*])")]
    private static partial Regex ItalicAsteriskRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExtraNewLinesRegex();
}
