using System.Text.Json.Serialization;

namespace Saturn.Bot.Service.Infrastructure.XaiChatClient.Model;

internal class XaiImageContent
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("image_url")]
    public required string ImageUrl { get; init; }
}
