using System.Text.Json.Serialization;

namespace Saturn.Bot.Service.Infrastructure.XaiChatClient.Model;

internal class XaiChatResponse
{
    [JsonPropertyName("output")]
    public List<XaiOutputItem>? Output { get; init; }
}
