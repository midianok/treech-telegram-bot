using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Configurations;

public class MemeTowerScoreEntityConfiguration : IEntityTypeConfiguration<MemeTowerScoreEntity>
{
    public void Configure(EntityTypeBuilder<MemeTowerScoreEntity> builder)
    {
        builder.HasKey(x => new { x.UserId, x.ChatId });

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId);
    }
}
