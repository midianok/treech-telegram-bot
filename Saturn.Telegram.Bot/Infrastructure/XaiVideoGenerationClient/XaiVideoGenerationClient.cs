using System.Net.Http.Json;
using System.Text.Json;

namespace Saturn.Bot.Service.Infrastructure.XaiVideoGenerationClient;

public class XaiVideoGenerationClient
{
    private readonly HttpClient _httpClient;

    public XaiVideoGenerationClient(HttpClient httpClient) =>
        _httpClient = httpClient;

    public async Task<byte[]> GenerateVideoFromImageAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = "grok-imagine-video",
            prompt = "Animate this image naturally. If any person speaks or mouths words, they must speak Russian.",
            image = new { url = $"data:image/jpeg;base64,{Convert.ToBase64String(imageBytes)}" },
            duration = 5,
            resolution = "480p"
        };

        var startResponse = await _httpClient.PostAsJsonAsync("v1/videos/generations", request, cancellationToken);
        startResponse.EnsureSuccessStatusCode();

        using var startDoc = await startResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        var requestId = startDoc?.RootElement.GetProperty("request_id").GetString()
            ?? throw new InvalidOperationException("Пустой request_id от xAI video API");

        while (true)
        {
            var pollResponse = await _httpClient.GetAsync($"v1/videos/{requestId}", cancellationToken);
            pollResponse.EnsureSuccessStatusCode();

            using var pollDoc = await pollResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
            var status = pollDoc?.RootElement.GetProperty("status").GetString();

            if (status == "done")
            {
                var videoUrl = pollDoc!.RootElement
                    .GetProperty("video")
                    .GetProperty("url")
                    .GetString()
                    ?? throw new InvalidOperationException("Пустой URL видео от xAI video API");

                var videoResponse = await _httpClient.GetAsync(videoUrl, cancellationToken);
                videoResponse.EnsureSuccessStatusCode();
                return await videoResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            }

            if (status == "failed" || status == "expired")
            {
                throw new InvalidOperationException($"Генерация видео завершилась со статусом: {status}");
            }
            
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }
}
