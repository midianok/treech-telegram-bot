namespace Saturn.Telegram.Db.Entities;

public enum CoinTransactionKind
{
    Topup = 0,
    Charge = 1,
    Refund = 2,
    Bonus = 3,
}

public class CoinTransactionEntity
{
    public Guid Id { get; set; }

    public long UserId { get; set; }

    public virtual UserEntity? User { get; set; }

    /// <summary>Signed amount in coins: positive credits the balance, negative debits it.</summary>
    public long Amount { get; set; }

    public CoinTransactionKind Kind { get; set; }

    /// <summary>Operation type name for <see cref="CoinTransactionKind.Charge"/>/<see cref="CoinTransactionKind.Refund"/>.</summary>
    public string? Operation { get; set; }

    /// <summary>Telegram payment charge id for <see cref="CoinTransactionKind.Topup"/>; unique, used for idempotency.</summary>
    public string? ExternalPaymentId { get; set; }

    public DateTime Date { get; set; }
}
