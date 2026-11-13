namespace Saturn.Bot.Service.Services.Tools;

/// <summary>Limits pictures the AI chat draws on request: one per user per period, like the «покажи» command.</summary>
public class ChatImageQuota
{
    public static readonly TimeSpan Period = TimeSpan.FromMinutes(10);

    private readonly Dictionary<long, DateTime> _lastUse = new();
    private readonly Lock _lock = new();

    public bool TryConsume(long userId, out TimeSpan wait)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            if (_lastUse.TryGetValue(userId, out var lastUse) && now - lastUse < Period)
            {
                wait = Period - (now - lastUse);
                return false;
            }

            _lastUse[userId] = now;
            wait = TimeSpan.Zero;
            return true;
        }
    }

    /// <summary>Gives the quota back after a failed generation.</summary>
    public void Release(long userId)
    {
        lock (_lock)
        {
            _lastUse.Remove(userId);
        }
    }
}
