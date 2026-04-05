using System.Text.Json.Serialization;

namespace Saturn.Bot.Service.Infrastructure.XaiImageEditClient.Model;

internal class XaiImageEditResponse
{
    [JsonPropertyName("data")]
    public List<XaiImageEditData>? Data { get; init; }
}
