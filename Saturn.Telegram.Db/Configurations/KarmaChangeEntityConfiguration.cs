using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Configurations;

public class KarmaChangeEntityConfiguration : IEntityTypeConfiguration<KarmaChangeEntity>
{
    public void Configure(EntityTypeBuilder<KarmaChangeEntity> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.FromUserId);
        builder.HasIndex(x => x.ToUserId);
        builder.HasIndex(x => x.CreatedAt);

        builder.HasOne(x => x.FromUser)
            .WithMany()
            .HasForeignKey(x => x.FromUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ToUser)
            .WithMany()
            .HasForeignKey(x => x.ToUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
