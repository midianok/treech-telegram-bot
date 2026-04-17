using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Configurations;

public class OperationCallEntityConfiguration : IEntityTypeConfiguration<OperationCallEntity>
{
    public void Configure(EntityTypeBuilder<OperationCallEntity> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OperationName)
            .HasMaxLength(256)
            .IsRequired();

    }
}
