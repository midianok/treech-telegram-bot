using Microsoft.EntityFrameworkCore;
using Saturn.Bot.Service.Database.Configurations;
using Saturn.Bot.Service.Database.Entities;

namespace Saturn.Bot.Service.Database;

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