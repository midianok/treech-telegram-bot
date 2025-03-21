using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Db.Configurations;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db;

public sealed class SaturnContext : DbContext
{
    public DbSet<MessageEntity> Messages { get; set; } = null!;
    
    public DbSet<UserEntity> Users { get; set; } = null!;
    
    public DbSet<ChatEntity> Chats { get; set; } = null!;

    public SaturnContext(DbContextOptions<SaturnContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AccountEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ChatEntityConfiguration());
        modelBuilder.ApplyConfiguration(new CooldownEntityConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceEntityConfiguration());
        modelBuilder.ApplyConfiguration(new MessageEntityConfiguration());
        modelBuilder.ApplyConfiguration(new RulesEntityConfiguration());
        modelBuilder.ApplyConfiguration(new UserEntityConfiguration());
    }
}