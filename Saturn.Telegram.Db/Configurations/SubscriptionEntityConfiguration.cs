using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Configurations;

public class SubscriptionEntityConfiguration : IEntityTypeConfiguration<SubscriptionEntity>
{
    public void Configure(EntityTypeBuilder<SubscriptionEntity> builder)
    {
        builder.HasKey(x =>  x.Id);
        
        builder.HasIndex(x => x.UserId);

        builder.HasOne(x => x.User)
            .WithMany(x=> x.Subscription)
            .HasForeignKey(x => x.UserId);
    }
}