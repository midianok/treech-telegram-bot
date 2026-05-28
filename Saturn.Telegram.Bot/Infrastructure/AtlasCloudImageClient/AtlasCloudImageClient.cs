using System.Net.Http.Json;
using System.Text.Json;

namespace Saturn.Bot.Service.Infrastructure.AtlasCloudImageClient;

public class AtlasCloudImageClient
{
    private readonly HttpClient _httpClient;

    public AtlasCloudImageClient(HttpClient httpClient) =>
        _httpClient = httpClient;

    public Task<byte[]> GenerateImageAsync(string prompt) =>
        ExecuteImageAsync(new
        {
            model = "bytedance/seedream-v5.0-lite",
            prompt,
            enable_base64_output = true
        });

    public Task<byte[]> EditImageAsync(IReadOnlyList<byte[]> imageBytesList, string prompt) =>
        ExecuteImageAsync(new
        {
            model = "bytedance/seedream-v5.0-lite/edit",
            prompt,
            images = imageBytesList.Select(b => $"data:image/jpeg;base64,{Convert.ToBase64String(b)}").ToArray(),
            enable_base64_output = true
        });

    public async Task<byte[]> GenerateVideoFromImageAsync(byte[] imageBytes, string? prompt, string aspectRatio, CancellationToken cancellationToken = default)
    {
        var effectivePrompt = string.IsNullOrWhiteSpace(prompt)
            ? "Animate this image naturally. If any person speaks or mouths words, they must speak Russian."
            : prompt;

        var request = new
        {
            model = "bytedance/seedance-2.0-fast/image-to-video",
            prompt = effectivePrompt,
            image = $"data:image/jpeg;base64,{Convert.ToBase64String(imageBytes)}",
            resolution = "720p",
            duration = 5,
            ratio = aspectRatio,
            generate_audio = true,
            watermark = false
        };

        var response = await _httpClient.PostAsJsonAsync("api/v1/model/generateVideo", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var startJson = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        var predictionId = startJson!.RootElement
            .GetProperty("data")
            .GetProperty("id")
            .GetString()
            ?? throw new InvalidOperationException("Не получен prediction ID от AtlasCloud video API");

        return await PollVideoResultAsync(predictionId, cancellationToken);
    }

    private async Task<byte[]> ExecuteImageAsync(object request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/model/generateImage", request);
        response.EnsureSuccessStatusCode();

        using var startJson = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var predictionId = startJson!.RootElement
            .GetProperty("data")
            .GetProperty("id")
            .GetString()
            ?? throw new InvalidOperationException("Не получен prediction ID от AtlasCloud");

        return await PollImageResultAsync(predictionId);
    }

    private async Task<byte[]> PollImageResultAsync(string predictionId)
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));

            var resultResponse = await _httpClient.GetAsync($"api/v1/model/result/{predictionId}");
            resultResponse.EnsureSuccessStatusCode();

            using var resultJson = await resultResponse.Content.ReadFromJsonAsync<JsonDocument>();
            var data = resultJson!.RootElement.GetProperty("data");
            var status = data.GetProperty("status").GetString();

            if (status is "completed" or "succeeded")
            {
                var b64 = data.GetProperty("outputs")[0].GetString()
                    ?? throw new InvalidOperationException("Пустой ответ от AtlasCloud");
                return Convert.FromBase64String(b64);
            }

            if (status == "failed")
                throw new InvalidOperationException("AtlasCloud вернул статус failed");
        }
    }

    private async Task<byte[]> PollVideoResultAsync(string predictionId, CancellationToken cancellationToken)
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

            var resultResponse = await _httpClient.GetAsync($"api/v1/model/result/{predictionId}", cancellationToken);
            resultResponse.EnsureSuccessStatusCode();

            using var resultJson = await resultResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
            var data = resultJson!.RootElement.GetProperty("data");
            var status = data.GetProperty("status").GetString();

            if (status is "completed" or "succeeded")
            {
                var videoUrl = data.GetProperty("outputs")[0].GetString()
                    ?? throw new InvalidOperationException("Пустой URL видео от AtlasCloud");

                var videoResponse = await _httpClient.GetAsync(videoUrl, cancellationToken);
                videoResponse.EnsureSuccessStatusCode();
                return await videoResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            }

            if (status == "failed")
                throw new InvalidOperationException("AtlasCloud вернул статус failed для видео");
        }
    }
}
