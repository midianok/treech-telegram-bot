using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Saturn.Bot.Service.Infrastructure.AtlasCloudImageClient;

public class AtlasCloudImageClient
{
    private const string TextToImageModel = "openai/gpt-image-2.5-flare/text-to-image";
    private const string TextToImageFallbackModel = "bytedance/seedream-v5.0-flash/text-to-image";
    private const string EditModel = "google/nano-banana/edit-developer";
    private const string EditFallbackModel = "bytedance/seedream-v5.0-flash/edit";
    private const string ImageToVideoModel = "bytedance/seedance-2.0-fast/image-to-video";

    private static readonly TimeSpan ImageTimeout = TimeSpan.FromMinutes(3);

    private readonly HttpClient _httpClient;
    private readonly ILogger<AtlasCloudImageClient> _logger;

    public AtlasCloudImageClient(HttpClient httpClient, ILogger<AtlasCloudImageClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<byte[]> GenerateImageAsync(string prompt, Action<string>? onStatusChanged = null) =>
        ExecuteImageWithFallbackAsync(
            prompt,
            0,
            TextToImageModel,
            new
            {
                model = TextToImageModel,
                prompt
            },
            TextToImageFallbackModel,
            new
            {
                model = TextToImageFallbackModel,
                prompt,
                enable_base64_output = true
            },
            onStatusChanged);

    public Task<byte[]> EditImageAsync(IReadOnlyList<byte[]> imageBytesList, string prompt, Action<string>? onStatusChanged = null)
    {
        var images = imageBytesList.Select(b => $"data:image/jpeg;base64,{Convert.ToBase64String(b)}").ToArray();

        return ExecuteImageWithFallbackAsync(
            prompt,
            images.Length,
            EditModel,
            new
            {
                model = EditModel,
                prompt,
                images,
                enable_base64_output = true
            },
            EditFallbackModel,
            new
            {
                model = EditFallbackModel,
                prompt,
                images,
                enable_base64_output = true
            },
            onStatusChanged);
    }

    public async Task<byte[]> GenerateVideoFromImageAsync(byte[] imageBytes, string? prompt, string aspectRatio, bool generateAudio = false, Action<string>? onStatusChanged = null, CancellationToken cancellationToken = default)
    {
        var effectivePrompt = string.IsNullOrWhiteSpace(prompt)
            ? "Animate this image naturally. If any person speaks or mouths words, they must speak Russian."
            : prompt;

        var request = new
        {
            model = ImageToVideoModel,
            prompt = effectivePrompt,
            image = $"data:image/jpeg;base64,{Convert.ToBase64String(imageBytes)}",
            resolution = "720p",
            duration = 5,
            ratio = aspectRatio,
            generate_audio = generateAudio,
            watermark = false
        };

        _logger.LogInformation(
            "AtlasCloud video request: model {Model}, ratio {AspectRatio}, audio {GenerateAudio}, prompt: {Prompt}",
            ImageToVideoModel, aspectRatio, generateAudio, effectivePrompt);

        var response = await _httpClient.PostAsJsonAsync("api/v1/model/generateVideo", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var startJson = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
        var predictionId = startJson!.RootElement
            .GetProperty("data")
            .GetProperty("id")
            .GetString()
            ?? throw new InvalidOperationException("Не получен prediction ID от AtlasCloud video API");

        _logger.LogInformation("AtlasCloud video prediction {PredictionId} started (model {Model})", predictionId, ImageToVideoModel);

        return await PollVideoResultAsync(predictionId, onStatusChanged, cancellationToken);
    }

    private async Task<byte[]> ExecuteImageWithFallbackAsync(
        string prompt,
        int imageCount,
        string primaryModel,
        object primaryRequest,
        string fallbackModel,
        object fallbackRequest,
        Action<string>? onStatusChanged)
    {
        try
        {
            return await ExecuteImageAsync(primaryModel, prompt, imageCount, primaryRequest, onStatusChanged);
        }
        catch (Exception ex) when (IsModerationRejection(ex))
        {
            _logger.LogWarning(
                "AtlasCloud model {Model} rejected request ({Reason}), falling back to {FallbackModel}",
                primaryModel, ex.Message, fallbackModel);

            return await ExecuteImageAsync(fallbackModel, prompt, imageCount, fallbackRequest, onStatusChanged);
        }
    }

    private static bool IsModerationRejection(Exception ex) =>
        ex switch
        {
            HttpRequestException { StatusCode: HttpStatusCode.BadRequest } => true,
            AtlasCloudPredictionFailedException { Status: "failed" } => true,
            _ => false
        };

    private async Task<byte[]> ExecuteImageAsync(string model, string prompt, int imageCount, object request, Action<string>? onStatusChanged)
    {
        _logger.LogInformation(
            "AtlasCloud image request: model {Model}, input images {ImageCount}, prompt: {Prompt}",
            model, imageCount, prompt);

        var response = await _httpClient.PostAsJsonAsync("api/v1/model/generateImage", request);
        response.EnsureSuccessStatusCode();

        using var startJson = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var predictionId = startJson!.RootElement
            .GetProperty("data")
            .GetProperty("id")
            .GetString()
            ?? throw new InvalidOperationException("Не получен prediction ID от AtlasCloud");

        _logger.LogInformation("AtlasCloud image prediction {PredictionId} started (model {Model})", predictionId, model);

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
