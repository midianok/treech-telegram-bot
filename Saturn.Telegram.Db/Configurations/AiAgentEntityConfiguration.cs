using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Configurations;

public class AiAgentEntityConfiguration : IEntityTypeConfiguration<AiAgentEntity>
{
    public void Configure(EntityTypeBuilder<AiAgentEntity> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Prompt)
            .IsRequired();

        builder.HasIndex(x => x.Code)
            .IsUnique();
        
        builder.Property(x => x.Code)
            .HasMaxLength(128)
            .IsRequired();
        
        builder.Property(x => x.Name)
            .HasMaxLength(128)
            .IsRequired();
        
        builder.HasMany(x => x.Chats)
            .WithOne(x => x.AiAgent)
            .HasForeignKey(x => x.AiAgentId);
    }
}