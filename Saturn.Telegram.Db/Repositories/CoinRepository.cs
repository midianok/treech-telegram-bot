using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Db.Repositories.Abstractions;

namespace Saturn.Telegram.Db.Repositories;

public class CoinRepository : ICoinRepository
{
    private readonly IDbContextFactory<SaturnContext> _dbContextFactory;

    public CoinRepository(IDbContextFactory<SaturnContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<long> GetBalanceAsync(long userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.CoinBalance)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> TryChargeAsync(long userId, long amount, string operation, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Conditional UPDATE locks the row and prevents overdraft under concurrency.
        var affected = await db.Users
            .Where(u => u.Id == userId && u.CoinBalance >= amount)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.CoinBalance, u => u.CoinBalance - amount), cancellationToken);

        if (affected == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        db.CoinTransactions.Add(new CoinTransactionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = -amount,
            Kind = CoinTransactionKind.Charge,
            Operation = operation,
            Date = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task RefundAsync(long userId, long amount, string operation, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await db.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.CoinBalance, u => u.CoinBalance + amount), cancellationToken);

        db.CoinTransactions.Add(new CoinTransactionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = amount,
            Kind = CoinTransactionKind.Refund,
            Operation = operation,
            Date = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> CreditFromPaymentAsync(long userId, long amount, string externalPaymentId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (await db.CoinTransactions.AnyAsync(t => t.ExternalPaymentId == externalPaymentId, cancellationToken))
        {
            return false;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.Users
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.CoinBalance, u => u.CoinBalance + amount), cancellationToken);

            db.CoinTransactions.Add(new CoinTransactionEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Amount = amount,
                Kind = CoinTransactionKind.Topup,
                ExternalPaymentId = externalPaymentId,
                Date = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Unique index on external_payment_id: a concurrent delivery already credited this charge.
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
    }
}
