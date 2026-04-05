using System.Text.Json.Serialization;

namespace Saturn.Bot.Service.Infrastructure.XaiChatClient.Model;

internal class XaiTextContent
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }
}
