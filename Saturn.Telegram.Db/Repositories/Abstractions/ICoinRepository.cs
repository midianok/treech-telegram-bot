namespace Saturn.Telegram.Db.Repositories.Abstractions;

public interface ICoinRepository
{
    Task<long> GetBalanceAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>Atomically debits <paramref name="amount"/> coins if the balance is sufficient. Returns false when it is not.</summary>
    Task<bool> TryChargeAsync(long userId, long amount, string operation, CancellationToken cancellationToken = default);

    /// <summary>Credits <paramref name="amount"/> coins back after a failed paid operation.</summary>
    Task RefundAsync(long userId, long amount, string operation, CancellationToken cancellationToken = default);

    /// <summary>Credits coins from a payment, idempotent by <paramref name="externalPaymentId"/>. Returns false on a duplicate.</summary>
    Task<bool> CreditFromPaymentAsync(long userId, long amount, string externalPaymentId, CancellationToken cancellationToken = default);
}
