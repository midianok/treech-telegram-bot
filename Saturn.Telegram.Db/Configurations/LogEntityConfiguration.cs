using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Configurations;

public class LogEntityConfiguration : IEntityTypeConfiguration<LogEntity>
{
    public void Configure(EntityTypeBuilder<LogEntity> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.Source);
        builder.HasIndex(x => x.CreatedAt);

        builder.Property(x => x.Source).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Level).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(4096).IsRequired();
        builder.Property(x => x.Data).HasColumnType("jsonb").IsRequired();
    }
}
