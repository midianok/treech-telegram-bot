using System.Net.Http.Json;
using Saturn.Bot.Service.Infrastructure.XaiImageGenerationClient.Model;

namespace Saturn.Bot.Service.Infrastructure.XaiImageGenerationClient;

public class XaiImageGenerationClient
{
    private readonly HttpClient _httpClient;

    public XaiImageGenerationClient(HttpClient httpClient) =>
        _httpClient = httpClient;

    public async Task<byte[]> GenerateImageAsync(string prompt)
    {
        var request = new XaiImageGenerationRequest
        {
            Model = "grok-imagine-image",
            ResponseFormat = "b64_json",
            Prompt = prompt
        };

        var response = await _httpClient.PostAsJsonAsync("v1/images/generations", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<XaiImageGenerationResponse>();
        var b64 = result?.Data?.FirstOrDefault()?.B64Json
            ?? throw new InvalidOperationException("Пустой ответ от xAI image generation API");

        return Convert.FromBase64String(b64);
    }
}
