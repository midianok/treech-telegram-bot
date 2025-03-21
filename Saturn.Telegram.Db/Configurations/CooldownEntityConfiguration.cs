using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Configurations;

public class CooldownEntityConfiguration : IEntityTypeConfiguration<CooldownEntity>
{
    public void Configure(EntityTypeBuilder<CooldownEntity> builder)
    {
        builder.HasKey(x => x.Id);
    }
}