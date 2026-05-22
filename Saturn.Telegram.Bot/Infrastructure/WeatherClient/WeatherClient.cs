namespace Saturn.Bot.Service.Infrastructure.WeatherClient;

public class WeatherClient
{
    private readonly HttpClient _httpClient;

    public WeatherClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<string> GetWeatherAsync(string location, CancellationToken ct = default)
    {
        try
        {
            return await _httpClient.GetStringAsync(
                $"{Uri.EscapeDataString(location)}?format=j1&lang=ru", ct);
        }
        catch (Exception ex)
        {
            return $$$"""{"error":"Не удалось получить погоду: {{{ex.Message}}}"}""";
        }
    }
}
