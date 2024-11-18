using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MessageEntity = Saturn.Bot.Service.Database.Entities.MessageEntity;

namespace Saturn.Bot.Service.Database.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<MessageEntity>
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