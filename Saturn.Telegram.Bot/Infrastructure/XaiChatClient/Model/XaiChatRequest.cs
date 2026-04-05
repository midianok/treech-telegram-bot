using System.Text.Json.Serialization;

namespace Saturn.Bot.Service.Infrastructure.XaiChatClient.Model;

internal class XaiChatRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("input")]
    public required List<XaiMessage> Input { get; init; }

    [JsonPropertyName("store")]
    public required bool Store { get; init; }
}
