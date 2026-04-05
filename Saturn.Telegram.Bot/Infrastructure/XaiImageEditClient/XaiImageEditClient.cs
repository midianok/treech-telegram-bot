using System.Net.Http.Json;
using System.Text.Json;

namespace Saturn.Bot.Service.Infrastructure.XaiImageEditClient;

public class XaiImageEditClient
{
    private readonly HttpClient _httpClient;

    public XaiImageEditClient(HttpClient httpClient) =>
        _httpClient = httpClient;

    public async Task<byte[]> EditImageAsync(byte[] imageBytes, string prompt)
    {
        var base64Image = Convert.ToBase64String(imageBytes);

        var request = new
        {
            model = "grok-imagine-image",
            prompt,
            image = new
            {
                type = "image_url",
                url = $"data:image/jpeg;base64,{base64Image}"
            },
            response_format = "b64_json"
        };

        var response = await _httpClient.PostAsJsonAsync("v1/images/edits", request);
        response.EnsureSuccessStatusCode();

        using var json = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var b64 = json?.RootElement
            .GetProperty("data")[0]
            .GetProperty("b64_json")
            .GetString()
            ?? throw new InvalidOperationException("Пустой ответ от xAI image edit API");

        return Convert.FromBase64String(b64);
    }
}