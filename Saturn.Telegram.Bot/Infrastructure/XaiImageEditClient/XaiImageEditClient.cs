using System.Net.Http.Json;
using Saturn.Bot.Service.Infrastructure.XaiImageEditClient.Model;

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
            Model = "grok-edit-image",
            ResponseFormat = "b64_json",
            Prompt = prompt,
            Image = new XaiImageSource
            {
                Type = "image_url",
                Url = $"data:image/jpeg;base64,{base64Image}"
            }
        };

        var response = await _httpClient.PostAsJsonAsync("v1/images/edits", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<XaiImageEditResponse>();
        var b64 = result?.Data?.FirstOrDefault()?.B64Json
            ?? throw new InvalidOperationException("Пустой ответ от xAI image edit API");

        return Convert.FromBase64String(b64);
    }
}
