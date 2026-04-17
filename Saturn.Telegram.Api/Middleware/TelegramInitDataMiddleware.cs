using System.Security.Cryptography;
using System.Text;

namespace Saturn.Telegram.Api.Middleware;

public class TelegramInitDataMiddleware(RequestDelegate next, IConfiguration config)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        var botToken = config["BOT_TOKEN"];
        var initData = ctx.Request.Headers["X-Telegram-Init-Data"].FirstOrDefault();
        if (string.IsNullOrEmpty(initData) || string.IsNullOrEmpty(botToken) || !Validate(initData, botToken))
        {
            ctx.Response.StatusCode = 401;
            return;
        }
        await next(ctx);
    }

    private static bool Validate(string initData, string botToken)
    {
        var pairs = initData.Split('&')
            .Select(p => p.Split('=', 2))
            .Where(p => p.Length == 2)
            .ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1]));

        if (!pairs.TryGetValue("hash", out var hash))
        {
            return false;
        }

        var p = pairs.Where(p => p.Key != "hash")
            .OrderBy(p => p.Key)
            .Select(p => $"{p.Key}={p.Value}");
        var dataCheckString = string.Join('\n', p);

        var secretKey = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));

        var expectedHash = Convert.ToHexString(HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(dataCheckString)));

        return string.Equals(expectedHash, hash, StringComparison.OrdinalIgnoreCase);
    }
}