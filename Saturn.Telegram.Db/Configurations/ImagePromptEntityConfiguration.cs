using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Configurations;

public class ImagePromptEntityConfiguration : IEntityTypeConfiguration<ImagePromptEntity>
{
    public void Configure(EntityTypeBuilder<ImagePromptEntity> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Keywords)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(x => x.Prompt)
            .IsRequired();
    }
}
