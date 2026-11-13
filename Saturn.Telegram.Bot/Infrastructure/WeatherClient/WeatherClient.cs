using Microsoft.Extensions.Logging;

namespace Saturn.Bot.Service.Infrastructure.WeatherClient;

public class WeatherClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WeatherClient> _logger;

    public WeatherClient(HttpClient httpClient, ILogger<WeatherClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> GetWeatherAsync(string location, CancellationToken сancellationToken)
    {
        try
        {
            return await _httpClient.GetStringAsync(
                $"{Uri.EscapeDataString(location)}?format=j1&lang=ru", сancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Weather request for {Location} failed", location);
            return $$$"""{"error":"Не удалось получить погоду: {{{ex.Message}}}"}""";
        }
    }
}
