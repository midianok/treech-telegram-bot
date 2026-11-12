using System.Text.Json;
using System.Text.RegularExpressions;

namespace Saturn.Bot.Service.Infrastructure.WikipediaClient;

public partial class WikipediaClient
{
    private const int MaxResults = 3;
    private const int MaxExtractLength = 1500;

    private readonly HttpClient _httpClient;

    public WikipediaClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<string> SearchAsync(string query, string? language, CancellationToken сancellationToken)
    {
        var lang = string.IsNullOrWhiteSpace(language) || !LanguageRegex().IsMatch(language)
            ? "ru"
            : language.ToLowerInvariant();

        var url = $"https://{lang}.wikipedia.org/w/api.php" +
                  "?action=query&format=json&formatversion=2&utf8=1" +
                  $"&generator=search&gsrsearch={Uri.EscapeDataString(query)}&gsrlimit={MaxResults}" +
                  $"&prop=extracts&exintro=1&explaintext=1&exlimit={MaxResults}";

        try
        {
            using var doc = JsonDocument.Parse(await _httpClient.GetStringAsync(url, сancellationToken));

            if (!doc.RootElement.TryGetProperty("query", out var queryElement) ||
                !queryElement.TryGetProperty("pages", out var pages))
            {
                return """{"results":[],"note":"Ничего не найдено"}""";
            }

            var results = pages.EnumerateArray()
                .OrderBy(x => x.TryGetProperty("index", out var index) ? index.GetInt32() : int.MaxValue)
                .Select(x =>
                {
                    var title = x.GetProperty("title").GetString() ?? string.Empty;
                    var extract = x.TryGetProperty("extract", out var extractElement)
                        ? extractElement.GetString() ?? string.Empty
                        : string.Empty;
                    if (extract.Length > MaxExtractLength)
                    {
                        extract = extract[..MaxExtractLength] + "…";
                    }

                    return new
                    {
                        title,
                        url = $"https://{lang}.wikipedia.org/wiki/{Uri.EscapeDataString(title.Replace(' ', '_'))}",
                        extract
                    };
                })
                .ToList();

            return JsonSerializer.Serialize(new { results });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = $"Не удалось выполнить поиск в Википедии: {ex.Message}" });
        }
    }

    [GeneratedRegex("^[a-zA-Z]{2,3}$")]
    private static partial Regex LanguageRegex();
}
