using System.Text.Json.Serialization;

namespace Saturn.Bot.Service.Infrastructure.XaiImageEditClient.Model;

internal class XaiImageEditRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }

    [JsonPropertyName("image")]
    public required XaiImageSource Image { get; init; }

    [JsonPropertyName("response_format")]
    public required string ResponseFormat { get; init; }
}
