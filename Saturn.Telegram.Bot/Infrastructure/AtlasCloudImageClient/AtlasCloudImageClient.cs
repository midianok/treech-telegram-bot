using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Saturn.Bot.Service.Infrastructure.AtlasCloudImageClient;

public class AtlasCloudImageClient
{
    private static readonly TimeSpan ImageTimeout = TimeSpan.FromMinutes(3);

    private readonly HttpClient _httpClient;

    public AtlasCloudImageClient(HttpClient httpClient) =>
        _httpClient = httpClient;

    public Task<byte[]> GenerateImageAsync(string prompt, Action<string>? onStatusChanged = null) =>
        ExecuteImageWithFallbackAsync(
            new
            {
                model = "openai/gpt-image-2.5-flare/text-to-image",
                prompt
            },
            new
            {
                model = "bytedance/seedream-v5.0-flash/text-to-image",
                prompt,
                enable_base64_output = true
            },
            onStatusChanged);

    public Task<byte[]> EditImageAsync(IReadOnlyList<byte[]> imageBytesList, string prompt, Action<string>? onStatusChanged = null)
    {
        var images = imageBytesList.Select(b => $"data:image/jpeg;base64,{Convert.ToBase64String(b)}").ToArray();

        return ExecuteImageWithFallbackAsync(
            new
            {
                model = "google/nano-banana/edit-developer",
                prompt,
                images,
                enable_base64_output = true
            },
            new
            {
                model = "bytedance/seedream-v5.0-flash/edit",
                prompt,
                images,
                enable_base64_output = true
            },
            onStatusChanged);
    }

    private async Task<byte[]> ExecuteImageWithFallbackAsync(object primaryRequest, object fallbackRequest, Action<string>? onStatusChanged)
    {
        try
        {
            return await ExecuteImageAsync(primaryRequest, onStatusChanged);
        }
        catch (Exception ex) when (IsModerationRejection(ex))
        {
            return await ExecuteImageAsync(fallbackRequest, onStatusChanged);
        }
    }

    private static bool IsModerationRejection(Exception ex) =>
        ex switch
        {
            HttpRequestException { StatusCode: HttpStatusCode.BadRequest } => true,
            AtlasCloudPredictionFailedException { Status: "failed" } => true,
            _ => false
        };

    public async Task<byte[]> GenerateVideoFromImageAsync(byte[] imageBytes, string? prompt, string aspectRatio, bool generateAudio = false, Action<string>? onStatusChanged = null, CancellationToken cancellationToken = default)
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
            generate_audio = generateAudio,
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

        return await PollVideoResultAsync(predictionId, onStatusChanged, cancellationToken);
    }

    private async Task<byte[]> ExecuteImageAsync(object request, Action<string>? onStatusChanged)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/model/generateImage", request);
        response.EnsureSuccessStatusCode();

        using var startJson = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var predictionId = startJson!.RootElement
            .GetProperty("data")
            .GetProperty("id")
            .GetString()
            ?? throw new InvalidOperationException("Не получен prediction ID от AtlasCloud");

        return await PollImageResultAsync(predictionId, onStatusChanged);
    }

    private async Task<byte[]> PollImageResultAsync(string predictionId, Action<string>? onStatusChanged)
    {
        var deadline = DateTime.UtcNow + ImageTimeout;

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));

            var resultResponse = await _httpClient.GetAsync($"api/v1/model/result/{predictionId}");
            resultResponse.EnsureSuccessStatusCode();

            using var resultJson = await resultResponse.Content.ReadFromJsonAsync<JsonDocument>();
            var data = resultJson!.RootElement.GetProperty("data");
            var status = data.GetProperty("status").GetString();

            if (status != null)
            {
                onStatusChanged?.Invoke(status);
            }

            if (status is "completed" or "succeeded")
            {
                var outputs = data.TryGetProperty("outputs", out var outputsElement) && outputsElement.ValueKind == JsonValueKind.Array
                    ? outputsElement
                    : default;

                var output = outputs.ValueKind == JsonValueKind.Array && outputs.GetArrayLength() > 0
                    ? outputs[0].GetString()
                    : null;

                if (string.IsNullOrEmpty(output))
                {
                    throw new AtlasCloudPredictionFailedException("failed", "empty outputs", $"AtlasCloud вернул пустой результат для изображения (prediction {predictionId})");
                }

                return await ReadImageOutputAsync(output);
            }

            ThrowIfTerminalFailure(data, status, "изображения");
        }

        throw new TimeoutException($"AtlasCloud не вернул изображение за {ImageTimeout.TotalMinutes} мин (prediction {predictionId})");
    }

    private async Task<byte[]> ReadImageOutputAsync(string output)
    {
        if (output.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || output.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var imageResponse = await _httpClient.GetAsync(output);
            imageResponse.EnsureSuccessStatusCode();
            return await imageResponse.Content.ReadAsByteArrayAsync();
        }

        var commaIndex = output.IndexOf(',');
        var b64 = output.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex >= 0
            ? output[(commaIndex + 1)..]
            : output;

        return Convert.FromBase64String(b64);
    }

    private async Task<byte[]> PollVideoResultAsync(string predictionId, Action<string>? onStatusChanged, CancellationToken cancellationToken)
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

            var resultResponse = await _httpClient.GetAsync($"api/v1/model/result/{predictionId}", cancellationToken);
            resultResponse.EnsureSuccessStatusCode();

            using var resultJson = await resultResponse.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
            var data = resultJson!.RootElement.GetProperty("data");
            var status = data.GetProperty("status").GetString();

            if (status != null)
            {
                onStatusChanged?.Invoke(status);
            }

            if (status is "completed" or "succeeded")
            {
                var videoUrl = data.GetProperty("outputs")[0].GetString()
                    ?? throw new InvalidOperationException("Пустой URL видео от AtlasCloud");

                var videoResponse = await _httpClient.GetAsync(videoUrl, cancellationToken);
                videoResponse.EnsureSuccessStatusCode();
                return await videoResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            }

            ThrowIfTerminalFailure(data, status, "видео");
        }
    }

    private static void ThrowIfTerminalFailure(JsonElement data, string? status, string subject)
    {
        if (status is not ("failed" or "timeout" or "canceled"))
        {
            return;
        }

        var error = data.TryGetProperty("error", out var errorElement) && errorElement.ValueKind == JsonValueKind.String
            ? errorElement.GetString()
            : null;

        throw new AtlasCloudPredictionFailedException(status, error, $"AtlasCloud вернул статус {status} для {subject}: {error}");
    }
}
