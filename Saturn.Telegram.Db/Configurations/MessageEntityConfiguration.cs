using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Configurations;

public class MessageEntityConfiguration : IEntityTypeConfiguration<MessageEntity>
{
    public void Configure(EntityTypeBuilder<MessageEntity> builder)
    {
        builder.HasKey(x => new { x.Id, x.ChatId });

        builder.HasIndex(x => x.ChatId);
        builder.HasIndex(x => new { x.UserId, x.ChatId });

        builder.Property(x => x.Text)
            .HasMaxLength(4096);
        
        builder.Property(x => x.StickerId)
            .HasMaxLength(256);

        builder.HasOne(x => x.Chat)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.ChatId);

        builder.HasOne(x => x.User)
            .WithMany(x => x.Messages)
            .HasForeignKey(x => x.UserId);
    }
}