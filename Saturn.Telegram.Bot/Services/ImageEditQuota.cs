using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Saturn.Bot.Service.Options;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Telegram.Bot.Types;

namespace Saturn.Bot.Service.Services;

public enum ImageEditCharge
{
    /// <summary>Neither a free edit nor enough coins left.</summary>
    Denied,
    Free,
    Paid,
    Unlimited
}

/// <summary>
/// Pays for an image edit: one free edit per user per hour, then coins. Shared by the «измени» command
/// and the AI chat, so the limit can't be bypassed by switching between them. The admin is not limited.
/// </summary>
public class ImageEditQuota
{
    public static readonly TimeSpan FreeEditPeriod = TimeSpan.FromHours(1);

    private readonly ICoinRepository _coinRepository;
    private readonly ISparkShop _sparkShop;
    private readonly IMemoryCache _memoryCache;
    private readonly string? _adminUsername;

    public ImageEditQuota(ICoinRepository coinRepository, ISparkShop sparkShop, IMemoryCache memoryCache,
        IOptions<BotOptions> botOptions)
    {
        _coinRepository = coinRepository;
        _sparkShop = sparkShop;
        _memoryCache = memoryCache;
        _adminUsername = botOptions.Value.AdminUsername;
    }

    public int Cost => _sparkShop.ImageEditCost;

    public async Task<ImageEditCharge> TryChargeAsync(User user, string operation, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_adminUsername) &&
            string.Equals(user.Username, _adminUsername, StringComparison.OrdinalIgnoreCase))
        {
            return ImageEditCharge.Unlimited;
        }

        var key = FreeEditKey(user.Id);
        if (!_memoryCache.TryGetValue(key, out _))
        {
            _memoryCache.Set(key, DateTimeOffset.UtcNow.Add(FreeEditPeriod), FreeEditPeriod);
            return ImageEditCharge.Free;
        }

        return await _coinRepository.TryChargeAsync(user.Id, Cost, operation, cancellationToken)
            ? ImageEditCharge.Paid
            : ImageEditCharge.Denied;
    }

    /// <summary>Gives the free edit or the coins back after a failed edit.</summary>
    public async Task RefundAsync(User user, ImageEditCharge charge, string operation,
        CancellationToken cancellationToken)
    {
        switch (charge)
        {
            case ImageEditCharge.Free:
                _memoryCache.Remove(FreeEditKey(user.Id));
                break;
            case ImageEditCharge.Paid:
                await _coinRepository.RefundAsync(user.Id, Cost, operation, cancellationToken);
                break;
        }
    }

    /// <summary>Time left until the user's free edit is available again.</summary>
    public TimeSpan GetFreeEditWait(User user)
    {
        if (!_memoryCache.TryGetValue(FreeEditKey(user.Id), out DateTimeOffset readyAt))
        {
            return TimeSpan.Zero;
        }

        var wait = readyAt - DateTimeOffset.UtcNow;
        return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
    }

    public Task<long> GetBalanceAsync(User user, CancellationToken cancellationToken) =>
        _coinRepository.GetBalanceAsync(user.Id, cancellationToken);

    private static string FreeEditKey(long userId) => $"free_edit:{userId}";
}