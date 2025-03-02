using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Entities_MessageEntity = Saturn.Telegram.Db.Entities.MessageEntity;
using MessageEntity = Saturn.Telegram.Db.Entities.MessageEntity;

namespace Saturn.Telegram.Db.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Entities_MessageEntity>
{
    public void Configure(EntityTypeBuilder<MessageEntity> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.FromUserId);
        builder.HasIndex(x => x.ChatId);

        builder.Property(x => x.UpdateData)
            .HasColumnType("jsonb");
    }
}