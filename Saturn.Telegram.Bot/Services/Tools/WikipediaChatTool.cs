using System.Text.Json;
using OpenAI.Chat;
using Saturn.Bot.Service.Infrastructure.WikipediaClient;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Bot.Service.Services.Prompts;

namespace Saturn.Bot.Service.Services.Tools;

public class WikipediaChatTool : IChatTool
{
    public string FunctionName => "search_wikipedia";

    public ChatTool Definition { get; } = PromptLibrary.Tool("search_wikipedia", new
    {
        type = "object",
        properties = new
        {
            query = new { type = "string" },
            language = new { type = "string" }
        },
        required = new[] { "query" }
    });

    private readonly WikipediaClient _wikipediaClient;

    public WikipediaChatTool(WikipediaClient wikipediaClient) => _wikipediaClient = wikipediaClient;

    public async Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken)
    {
        using var doc = JsonDocument.Parse(arguments);
        if (!doc.RootElement.TryGetProperty("query", out var queryElement) ||
            string.IsNullOrWhiteSpace(queryElement.GetString()))
        {
            return """{"error":"Не указан поисковый запрос"}""";
        }

        var language = doc.RootElement.TryGetProperty("language", out var languageElement)
            ? languageElement.GetString()
            : null;

        return await _wikipediaClient.SearchAsync(queryElement.GetString()!, language, сancellationToken);
    }
}
