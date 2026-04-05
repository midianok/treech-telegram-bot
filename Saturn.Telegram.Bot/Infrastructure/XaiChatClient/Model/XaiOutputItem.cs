using System.Text.Json.Serialization;

namespace Saturn.Bot.Service.Infrastructure.XaiChatClient.Model;

internal class XaiOutputItem
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("content")]
    public List<XaiContentItem>? Content { get; init; }
}
