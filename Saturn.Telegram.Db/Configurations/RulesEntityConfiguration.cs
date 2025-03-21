using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Configurations;

public class RulesEntityConfiguration : IEntityTypeConfiguration<RulesEntity>
{
    public void Configure(EntityTypeBuilder<RulesEntity> builder)
    {
        builder.HasKey(x => x.Id);
    }
}