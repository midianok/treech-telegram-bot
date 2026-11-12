using System.Text.Json;
using OpenAI.Chat;
using Saturn.Bot.Service.Infrastructure.WikipediaClient;
using Saturn.Bot.Service.Services.Abstractions;

namespace Saturn.Bot.Service.Services.Tools;

public class WikipediaChatTool : IChatTool
{
    public string FunctionName => "search_wikipedia";

    public ChatTool Definition { get; } = ChatTool.CreateFunctionTool(
        functionName: "search_wikipedia",
        functionDescription: "Поиск по Википедии. Возвращает вводные части до 3 найденных статей. " +
                             "Обязательно используй, когда вопрос касается проверяемых фактов: дат, исторических событий, " +
                             "биографий, чисел, географии, определений — вместо ответа по памяти. " +
                             "Не используй для болтовни, мнений и шуток.",
        functionParameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                query = new
                {
                    type = "string",
                    description = "Короткий поисковый запрос из ключевых слов, например «Крещение Руси» или «Гагарин полёт»"
                },
                language = new
                {
                    type = "string",
                    description = "Код языка Википедии: ru (по умолчанию) или en для тем, которые лучше описаны в английской Википедии"
                }
            },
            required = new[] { "query" }
        })
    );

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
