using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Saturn.Telegram.Api.Middleware;

public class TelegramInitDataMiddleware(RequestDelegate next, IConfiguration config)
{
    public const string UserIdItemKey = "TelegramUserId";

    public async Task InvokeAsync(HttpContext ctx)
    {
        var botToken = config["BOT_TOKEN"];
        var initData = ctx.Request.Headers["X-Telegram-Init-Data"].FirstOrDefault();
        if (string.IsNullOrEmpty(initData) || string.IsNullOrEmpty(botToken))
        {
            ctx.Response.StatusCode = 401;
            return;
        }

        if (!TryValidate(initData, botToken, out var userId))
        {
            ctx.Response.StatusCode = 401;
            return;
        }

        if (userId.HasValue)
        {
            ctx.Items[UserIdItemKey] = userId.Value;
        }

        await next(ctx);
    }

    private static bool TryValidate(string initData, string botToken, out long? userId)
    {
        userId = null;

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
                if (doc.RootElement.TryGetProperty("id", out var idProp) && idProp.TryGetInt64(out var id))
                    userId = id;
            }
            catch (JsonException) { }
        }

        return true;
    }
}