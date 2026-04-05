using System.Text.Json.Serialization;

namespace Saturn.Bot.Service.Infrastructure.XaiImageGenerationClient.Model;

internal class XaiImageGenerationRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("prompt")]
    public required string Prompt { get; init; }

    [JsonPropertyName("response_format")]
    public required string ResponseFormat { get; init; }
}
