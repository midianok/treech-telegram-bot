using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace Saturn.Bot.Service.Extension;

public static class Extension
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions =
        new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault };

    public static string ToJson<T>(this T obj) where T : class =>
        JsonSerializer.Serialize(obj, _jsonSerializerOptions);

    public static string GetSectionOrThrow(this IConfiguration configuration, string key)
    {
        var item = configuration.GetSection(key).Value;
        if (string.IsNullOrWhiteSpace(item))
        {
            throw new Exception($"configuration item {key} not presented");
        }

        return item;
    }
}