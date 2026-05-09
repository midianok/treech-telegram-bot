using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Configurations;

public class NamorevoGoreScoreEntityConfiguration : IEntityTypeConfiguration<NamorevoGoreScoreEntity>
{
    public void Configure(EntityTypeBuilder<NamorevoGoreScoreEntity> builder)
    {
        builder.HasKey(x => x.UserId);

        builder.HasOne(x => x.User)
            .WithOne()
            .HasForeignKey<NamorevoGoreScoreEntity>(x => x.UserId);
    }
}
