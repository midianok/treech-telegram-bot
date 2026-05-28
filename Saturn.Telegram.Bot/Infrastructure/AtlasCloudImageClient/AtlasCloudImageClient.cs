using System.Net.Http.Json;
using System.Text.Json;

namespace Saturn.Bot.Service.Infrastructure.AtlasCloudImageClient;

public class AtlasCloudImageClient
{
    private readonly HttpClient _httpClient;

    public AtlasCloudImageClient(HttpClient httpClient) =>
        _httpClient = httpClient;

    public Task<byte[]> GenerateImageAsync(string prompt) =>
        ExecuteAsync(new
        {
            model = "bytedance/seedream-v5.0-lite",
            prompt,
            enable_base64_output = true
        });

    public Task<byte[]> EditImageAsync(IReadOnlyList<byte[]> imageBytesList, string prompt) =>
        ExecuteAsync(new
        {
            model = "bytedance/seedream-v5.0-lite/edit",
            prompt,
            images = imageBytesList.Select(b => $"data:image/jpeg;base64,{Convert.ToBase64String(b)}").ToArray(),
            enable_base64_output = true
        });

    private async Task<byte[]> ExecuteAsync(object request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/model/generateImage", request);
        response.EnsureSuccessStatusCode();

        using var startJson = await response.Content.ReadFromJsonAsync<JsonDocument>();
        var predictionId = startJson!.RootElement
            .GetProperty("data")
            .GetProperty("id")
            .GetString()
            ?? throw new InvalidOperationException("Не получен prediction ID от AtlasCloud");

        return await PollResultAsync(predictionId);
    }

    private async Task<byte[]> PollResultAsync(string predictionId)
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
}
