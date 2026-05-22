using System.Text.Json;
using OpenAI.Chat;
using Saturn.Bot.Service.Infrastructure.WeatherClient;
using Saturn.Bot.Service.Services.Abstractions;

namespace Saturn.Bot.Service.Services.Tools;

public class WeatherChatTool : IChatTool
{
    public string FunctionName => "get_weather";

    public ChatTool Definition { get; } = ChatTool.CreateFunctionTool(
        functionName: "get_weather",
        functionDescription: "Получить текущую погоду для указанного города или локации",
        functionParameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                location = new
                {
                    type = "string",
                    description = "Название города или локации на русском или английском языке"
                }
            },
            required = new[] { "location" }
        })
    );

    private readonly WeatherClient _weatherClient;

    public WeatherChatTool(WeatherClient weatherClient) => _weatherClient = weatherClient;

    public async Task<string> ExecuteAsync(string arguments, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(arguments);
        if (!doc.RootElement.TryGetProperty("location", out var locationElement))
        {
            return """{"error":"Не указана локация"}""";
        }
        return await _weatherClient.GetWeatherAsync(locationElement.GetString() ?? string.Empty, ct);
    }
}
