using System.Text.Json.Serialization;

namespace Saturn.Bot.Service.Infrastructure.XaiImageGenerationClient.Model;

internal class XaiImageGenerationResponse
{
    [JsonPropertyName("data")]
    public List<XaiImageGenerationData>? Data { get; init; }
}
