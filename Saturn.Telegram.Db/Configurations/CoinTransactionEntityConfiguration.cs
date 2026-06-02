using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Configurations;

public class CoinTransactionEntityConfiguration : IEntityTypeConfiguration<CoinTransactionEntity>
{
    public void Configure(EntityTypeBuilder<CoinTransactionEntity> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Operation)
            .HasMaxLength(256);

        builder.Property(x => x.ExternalPaymentId)
            .HasMaxLength(256);

        // Postgres treats NULLs as distinct, so charges/refunds (null id) coexist while
        // top-ups stay unique per Telegram payment charge — the idempotency guard.
        builder.HasIndex(x => x.ExternalPaymentId)
            .IsUnique();

        builder.HasIndex(x => x.UserId);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
