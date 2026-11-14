using Microsoft.Extensions.Options;
using Saturn.Bot.Service.Options;
using Telegram.Bot.Types;

namespace Saturn.Bot.Service.Services;

/// <summary>
/// Limits picture generation to one per user per period. Shared by the «покажи» command and the AI chat,
/// so the limit can't be bypassed by switching between them. The admin is not limited.
/// </summary>
public class ImageGenerationQuota
{
    public static readonly TimeSpan Period = TimeSpan.FromMinutes(10);

    private readonly Dictionary<long, DateTime> _lastUse = new();
    private readonly Lock _lock = new();
    private readonly string? _adminUsername;

    public ImageGenerationQuota(IOptions<BotOptions> botOptions)
    {
        _adminUsername = botOptions.Value.AdminUsername;
    }

    public bool TryConsume(User user, out TimeSpan wait)
    {
        wait = TimeSpan.Zero;
        if (!string.IsNullOrEmpty(_adminUsername) && string.Equals(user.Username, _adminUsername, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        lock (_lock)
        {
            var now = DateTime.UtcNow;
            if (_lastUse.TryGetValue(user.Id, out var lastUse) && now - lastUse < Period)
            {
                wait = Period - (now - lastUse);
                return false;
            }

            _lastUse[user.Id] = now;
            return true;
        }
    }

    /// <summary>Gives the quota back after a failed generation.</summary>
    public void Release(User user)
    {
        lock (_lock)
        {
            _lastUse.Remove(user.Id);
        }
    }
}
