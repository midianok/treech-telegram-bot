using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Configurations;

public class GamePointsEntityConfiguration : IEntityTypeConfiguration<GamePointsEntity>
{
    public void Configure(EntityTypeBuilder<GamePointsEntity> builder)
    {
        builder.HasKey(x => new { x.UserId, x.ChatId });

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId);
    }
}
