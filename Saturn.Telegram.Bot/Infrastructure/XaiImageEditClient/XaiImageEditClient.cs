using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Saturn.Bot.Service.Infrastructure.XaiImageEditClient;

public class XaiImageEditClient
{
    private readonly HttpClient _httpClient;

    public XaiImageEditClient(HttpClient httpClient) =>
        _httpClient = httpClient;

    public async Task<byte[]> EditImageAsync(byte[] imageBytes, string prompt)
    {
        var base64Image = Convert.ToBase64String(imageBytes);

        var request = new XaiImageEditRequest
        {
            Prompt = prompt,
            Image = new XaiImageSource
            {
                Url = $"data:image/jpeg;base64,{base64Image}"
            },
            ResponseFormat = "b64_json"
        };

        var response = await _httpClient.PostAsJsonAsync("v1/images/edits", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<XaiImageEditResponse>();
        var b64 = result?.Data?.FirstOrDefault()?.B64Json
            ?? throw new InvalidOperationException("Пустой ответ от xAI image edit API");

        return Convert.FromBase64String(b64);
    }
}

file class XaiImageEditRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = "grok-imagine-image";

    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }

    [JsonPropertyName("image")]
    public required XaiImageSource Image { get; init; }

    [JsonPropertyName("response_format")]
    public string ResponseFormat { get; init; } = "b64_json";
}

file class XaiImageSource
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = "image_url";
}

file class XaiImageEditResponse
{
    [JsonPropertyName("data")]
    public List<XaiImageEditData>? Data { get; init; }
}

file class XaiImageEditData
{
    [JsonPropertyName("b64_json")]
    public string? B64Json { get; init; }
}
