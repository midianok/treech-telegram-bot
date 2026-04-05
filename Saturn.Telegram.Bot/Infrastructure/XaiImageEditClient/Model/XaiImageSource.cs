using System.Text.Json.Serialization;

namespace Saturn.Bot.Service.Infrastructure.XaiImageEditClient.Model;

internal class XaiImageSource
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("type")]
    public required string Type { get; init; }
}
