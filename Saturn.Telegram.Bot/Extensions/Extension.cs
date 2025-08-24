using Microsoft.Extensions.Configuration;

namespace Saturn.Bot.Service.Extensions;

public static class Extension
{
    public static string GetSectionOrThrow(this IConfiguration configuration, string key)
    {
        var item = configuration.GetSection(key).Value;
        if (string.IsNullOrWhiteSpace(item))
        {
            throw new Exception($"Configuration item {key} not presented");
        }

        return item;
    }
    
    public static long ParseLong(this string? longString)
    {
        if (long.TryParse(longString, out long result))
        {
            return result;
        }
        throw new Exception($"Unable to parse long string {longString}");
    }
}