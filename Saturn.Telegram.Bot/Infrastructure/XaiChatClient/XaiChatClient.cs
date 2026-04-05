using System.Net.Http.Json;
using Saturn.Bot.Service.Infrastructure.XaiChatClient.Model;

namespace Saturn.Bot.Service.Infrastructure.XaiChatClient;

public class XaiChatClient
{
    private readonly HttpClient _httpClient;

    public XaiChatClient(HttpClient httpClient) =>
        _httpClient = httpClient;

    public async Task<string?> CompleteChatAsync(IEnumerable<XaiMessage> messages)
    {
        var request = new XaiChatRequest
        {
            Model = "grok-4-1-fast-non-reasoning",
            Store = false,
            Input = messages.ToList()
        };

        var response = await _httpClient.PostAsJsonAsync("v1/responses", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<XaiChatResponse>();
        return result?.Output?
            .Where(x => x.Type == "message")
            .SelectMany(x => x.Content ?? [])
            .FirstOrDefault(x => x.Type == "output_text")
            ?.Text;
    }
}
