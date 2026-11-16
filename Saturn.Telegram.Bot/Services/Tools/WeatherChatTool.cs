using System.Text.Json;
using OpenAI.Chat;
using Saturn.Bot.Service.Infrastructure.WeatherClient;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Bot.Service.Services.Prompts;

namespace Saturn.Bot.Service.Services.Tools;

public class WeatherChatTool : IChatTool
{
    public string FunctionName => "get_weather";

    public ChatTool Definition { get; } = PromptLibrary.Tool("get_weather", new
    {
        type = "object",
        properties = new
        {
            location = new { type = "string" }
        },
        required = new[] { "location" }
    });

    private readonly WeatherClient _weatherClient;

    public WeatherChatTool(WeatherClient weatherClient) => _weatherClient = weatherClient;

    public async Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken)
    {
        using var doc = JsonDocument.Parse(arguments);
        if (!doc.RootElement.TryGetProperty("location", out var locationElement))
        {
            return """{"error":"Не указана локация"}""";
        }
        return await _weatherClient.GetWeatherAsync(locationElement.GetString() ?? string.Empty, сancellationToken);
    }
}
