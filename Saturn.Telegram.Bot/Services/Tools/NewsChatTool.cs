using System.Text.Json;
using OpenAI.Chat;
using Saturn.Bot.Service.Infrastructure.NewsClient;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Bot.Service.Services.Tools.Utils;
using Saturn.Bot.Service.Services.Prompts;

namespace Saturn.Bot.Service.Services.Tools;

public class NewsChatTool : IChatTool
{
    public string FunctionName => "get_news";

    public ChatTool Definition { get; } = PromptLibrary.Tool("get_news", new
    {
        type = "object",
        properties = new
        {
            query = new { type = "string" },
            limit = new { type = "integer" }
        }
    }, new { defaultLimit = NewsClient.DefaultLimit, maxLimit = NewsClient.MaxLimit });

    private readonly NewsClient _newsClient;

    public NewsChatTool(NewsClient newsClient) => _newsClient = newsClient;

    public async Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken)
    {
        using var doc = JsonDocument.Parse(arguments);
        var query = ToolArguments.GetString(doc.RootElement, "query");
        var limit = (int)(ToolArguments.GetLong(doc.RootElement, "limit") ?? NewsClient.DefaultLimit);

        return await _newsClient.GetNewsAsync(query, limit, сancellationToken);
    }
}