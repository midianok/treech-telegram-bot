using Telegram.Bot.Types;

namespace Saturn.Bot.Service.Services.Abstractions;

public interface ISparkShop
{
    /// <summary>Price of a single paid image edit, in coins ("трич койны").</summary>
    int ImageEditCost { get; }

    Task<long> GetBalanceAsync(long userId, CancellationToken cancellationToken);

    /// <summary>Replies with <paramref name="reason"/> and the top-up bundle buttons.</summary>
    Task SendOfferAsync(Message msg, string reason, CancellationToken cancellationToken);

    /// <summary>Handles "buy" callbacks and pre-checkout queries. Returns true when the update was a shop update.</summary>
    Task<bool> TryHandleUpdateAsync(Update update, CancellationToken cancellationToken);

    Task HandleSuccessfulPaymentAsync(Message msg, CancellationToken cancellationToken);
}
