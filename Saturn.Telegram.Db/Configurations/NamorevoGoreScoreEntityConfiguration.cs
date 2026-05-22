using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Configurations;

public class NamorevoGoreScoreEntityConfiguration : IEntityTypeConfiguration<NamorevoGoreScoreEntity>
{
    public void Configure(EntityTypeBuilder<NamorevoGoreScoreEntity> builder)
    {
        builder.HasKey(x => new { x.UserId, x.ChatId });

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId);
    }
}
