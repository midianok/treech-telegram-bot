using System.Text.Json;
using System.Text.Json.Serialization;

namespace Saturn.Bot.Service.Extension;

public static class JsonExtension
{
    public static string ToJson<T>(this T obj) where T : class =>
        JsonSerializer.Serialize(obj, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault });
}