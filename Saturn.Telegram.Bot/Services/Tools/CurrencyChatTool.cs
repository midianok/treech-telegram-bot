using System.Text.Json;
using OpenAI.Chat;
using Saturn.Bot.Service.Infrastructure.CurrencyClient;
using Saturn.Bot.Service.Services.Abstractions;

namespace Saturn.Bot.Service.Services.Tools;

public class CurrencyChatTool : IChatTool
{
    public string FunctionName => "get_exchange_rate";

    public ChatTool Definition { get; } = ChatTool.CreateFunctionTool(
        functionName: "get_exchange_rate",
        functionDescription: "Получить актуальный курс валют",
        functionParameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                from = new
                {
                    type = "string",
                    description = "Базовая валюта (ISO 4217, например USD, EUR, RUB)"
                },
                to = new
                {
                    type = "array",
                    items = new { type = "string" },
                    description = "Целевые валюты (ISO 4217)"
                }
            },
            required = new[] { "from", "to" }
        })
    );

    private readonly CurrencyClient _currencyClient;

    public CurrencyChatTool(CurrencyClient currencyClient) => _currencyClient = currencyClient;

    public async Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken)
    {
        using var doc = JsonDocument.Parse(arguments);
        if (!doc.RootElement.TryGetProperty("from", out var fromElement))
        {
            return """{"error":"Не указана базовая валюта"}""";
        }
        var to = doc.RootElement.TryGetProperty("to", out var toElement)
            ? toElement.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray()
            : [];
        return await _currencyClient.GetRatesAsync(fromElement.GetString() ?? string.Empty, to, сancellationToken);
    }
}
