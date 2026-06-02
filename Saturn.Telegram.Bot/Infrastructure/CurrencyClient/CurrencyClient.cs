using System.Text.Json;

namespace Saturn.Bot.Service.Infrastructure.CurrencyClient;

public class CurrencyClient
{
    private readonly HttpClient _httpClient;

    public CurrencyClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<string> GetRatesAsync(string from, string[] to, CancellationToken сancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(
                await _httpClient.GetStringAsync($"v6/latest/{Uri.EscapeDataString(from.ToUpper())}", сancellationToken));

            if (!doc.RootElement.TryGetProperty("rates", out var rates))
            {
                return """{"error":"Не удалось получить курсы валют"}""";
            }

            var filteredRates = new Dictionary<string, double>();
            foreach (var currency in to)
            {
                var key = currency.ToUpper();
                if (rates.TryGetProperty(key, out var rate))
                {
                    filteredRates[key] = rate.GetDouble();
                }
            }

            return JsonSerializer.Serialize(new { base_currency = from.ToUpper(), rates = filteredRates });
        }
        catch (Exception ex)
        {
            return $$$"""{"error":"Не удалось получить курсы валют: {{{ex.Message}}}"}""";
        }
    }
}
