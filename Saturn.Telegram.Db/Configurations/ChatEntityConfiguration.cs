using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Configurations;

public class ChatEntityConfiguration : IEntityTypeConfiguration<ChatEntity>
{
    public void Configure(EntityTypeBuilder<ChatEntity> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.HasOne(x => x.AiAgent)
            .WithMany(x => x.Chats)
            .HasForeignKey(x => x.AiAgentId);
    }
}