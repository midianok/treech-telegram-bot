using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Saturn.Telegram.Api.Middleware;

public class TelegramInitDataMiddleware(RequestDelegate next, IConfiguration config)
{
    public const string UserIdItemKey = "TelegramUserId";
    public const string UserItemKey = "TelegramUser";

    public async Task InvokeAsync(HttpContext ctx)
    {
        var botToken = config["BOT_TOKEN"];
        var initData = ctx.Request.Headers["X-Telegram-Init-Data"].FirstOrDefault();
        if (string.IsNullOrEmpty(initData) || string.IsNullOrEmpty(botToken))
        {
            ctx.Response.StatusCode = 401;
            return;
        }

        if (!TryValidate(initData, botToken, out var userId, out var user))
        {
            ctx.Response.StatusCode = 401;
            return;
        }

        if (userId.HasValue)
        {
            ctx.Items[UserIdItemKey] = userId.Value;
        }

        if (user is not null)
        {
            ctx.Items[UserItemKey] = user;
        }

        await next(ctx);
    }

    private static bool TryValidate(string initData, string botToken, out long? userId, out TelegramInitDataUser? user)
    {
        userId = null;
        user = null;

        var pairs = initData.Split('&')
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1]));

        if (!pairs.TryGetValue("hash", out var hash))
            return false;

        if (!pairs.TryGetValue("auth_date", out var authDateStr)
            || !long.TryParse(authDateStr, out var unix)
            || DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unix) > TimeSpan.FromHours(24))
        {
            return false;
        }

        var dataCheckString = string.Join('\n', pairs
            .Where(p => p.Key != "hash")
            .OrderBy(p => p.Key)
            .Select(p => $"{p.Key}={p.Value}"));

        var secretKey = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
        var expectedHashBytes = HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(dataCheckString));

        if (!CryptographicOperations.FixedTimeEquals(expectedHashBytes, Convert.FromHexString(hash)))
            return false;

        if (pairs.TryGetValue("user", out var userJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(userJson);
                var root = doc.RootElement;
                if (root.TryGetProperty("id", out var idProp) && idProp.TryGetInt64(out var id))
                {
                    userId = id;
                    user = new TelegramInitDataUser(id,
                        GetString(root, "username"),
                        GetString(root, "first_name"),
                        GetString(root, "last_name"));
                }
            }
            catch (JsonException) { }
        }

        return true;
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

/// <summary>Пользователь из проверенного initData Mini App.</summary>
public record TelegramInitDataUser(long Id, string? Username, string? FirstName, string? LastName);