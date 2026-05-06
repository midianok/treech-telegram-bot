using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Configurations;

public class UserKarmaEntityConfiguration : IEntityTypeConfiguration<UserKarmaEntity>
{
    public void Configure(EntityTypeBuilder<UserKarmaEntity> builder)
    {
        builder.HasKey(x => x.UserId);

        builder.HasOne(x => x.User)
            .WithOne()
            .HasForeignKey<UserKarmaEntity>(x => x.UserId);
    }
}
