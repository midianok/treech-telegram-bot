using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Saturn.Bot.Service.Services.Tools.Utils;

namespace Saturn.Bot.Service.Infrastructure.NewsClient;

/// <summary>Reads the latest headlines from the TASS RSS feed.</summary>
public partial class NewsClient
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 40;

    private const string FeedUrl = "https://tass.ru/rss/v2.xml";

    private readonly HttpClient _httpClient;
    private readonly ILogger<NewsClient> _logger;

    public NewsClient(HttpClient httpClient, ILogger<NewsClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> GetNewsAsync(string? query, int limit, CancellationToken сancellationToken)
    {
        try
        {
            var feed = XDocument.Parse(await _httpClient.GetStringAsync(FeedUrl, сancellationToken));

            var items = feed.Descendants("item")
                .Select(ParseItem)
                .Where(x => x.Title.Length > 0)
                .ToList();

            if (query != null)
            {
                var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(ToStem)
                    .ToList();
                items = items
                    .Where(x => words.Any(w => x.Title.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                                               x.Summary.Contains(w, StringComparison.OrdinalIgnoreCase) ||
                                               x.Categories.Any(c =>
                                                   c.Contains(w, StringComparison.OrdinalIgnoreCase))))
                    .ToList();
            }

            if (items.Count == 0)
            {
                return ToolArguments.Result(new
                    { news = Array.Empty<object>(), note = "Среди свежих новостей ничего подходящего не нашлось" });
            }

            var news = items
                .Take(Math.Clamp(limit, 1, MaxLimit))
                .Select(x => new
                {
                    published = x.Published,
                    categories = x.Categories,
                    title = x.Title,
                    summary = x.Summary,
                    url = x.Url
                })
                .ToList();

            return ToolArguments.Result(new { source = "ТАСС", news });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "News feed request for {Query} failed", query);
            return ToolArguments.Error($"Не удалось получить новости: {ex.Message}");
        }
    }

    /// <summary>Cuts off a likely Russian ending, so "рубля" also finds "рубль" and "рублём".</summary>
    private static string ToStem(string word) => word.Length > 5 ? word[..^2] : word.Length > 3 ? word[..^1] : word;

    private static NewsItem ParseItem(XElement item)
    {
        string? published = null;
        if (ReminderFormatting.TryGetTimeZone(null, out var moscow) &&
            DateTimeOffset.TryParse(item.Element("pubDate")?.Value, CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var pubDate))
        {
            published = TimeZoneInfo.ConvertTime(pubDate, moscow)
                .ToString(ReminderFormatting.DateTimeFormat, CultureInfo.InvariantCulture) + " МСК";
        }

        return new NewsItem(
            CleanText(item.Element("title")?.Value),
            CleanText(item.Element("description")?.Value),
            item.Element("link")?.Value.Trim() ?? string.Empty,
            published,
            item.Elements("category").Select(x => CleanText(x.Value)).Where(x => x.Length > 0).ToList());
    }

    private static string CleanText(string? value) =>
        WhitespaceRegex().Replace(WebUtility.HtmlDecode(value ?? string.Empty), " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private record NewsItem(string Title, string Summary, string Url, string? Published, List<string> Categories);
}