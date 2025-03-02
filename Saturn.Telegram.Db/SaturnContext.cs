using Microsoft.EntityFrameworkCore;
using Saturn.Telegram.Db.Configurations;
using Saturn.Telegram.Db.Entities;

namespace Saturn.Telegram.Db;

public sealed class SaturnContext : DbContext
{
    public DbSet<MessageEntity> Messages { get; set; } = null!;

    public SaturnContext(DbContextOptions<SaturnContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new MessageConfiguration());
    }
}