using System.Text.Json.Serialization;

namespace Saturn.Bot.Service.Infrastructure.XaiImageEditClient.Model;

internal class XaiImageEditData
{
    [JsonPropertyName("b64_json")]
    public string? B64Json { get; init; }
}
