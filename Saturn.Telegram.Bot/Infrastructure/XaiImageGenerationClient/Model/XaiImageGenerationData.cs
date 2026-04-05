using System.Text.Json.Serialization;

namespace Saturn.Bot.Service.Infrastructure.XaiImageGenerationClient.Model;

internal class XaiImageGenerationData
{
    [JsonPropertyName("b64_json")]
    public string? B64Json { get; init; }
}
