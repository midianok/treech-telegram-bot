using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db.Configurations;

public class ReminderEntityConfiguration : IEntityTypeConfiguration<ReminderEntity>
{
    public void Configure(EntityTypeBuilder<ReminderEntity> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Text)
            .HasMaxLength(1000);

        // The sender polls for due reminders, so only pending ones need to be indexed
        builder.HasIndex(x => x.RemindAt)
            .HasFilter("sent_at IS NULL");

        builder.HasIndex(x => new { x.ChatId, x.CreatedByUserId });

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
