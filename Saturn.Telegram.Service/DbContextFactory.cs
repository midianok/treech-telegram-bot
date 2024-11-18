using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Saturn.Bot.Service.Database;

namespace Saturn.Bot.Service;

public class DbContextFactory : IDesignTimeDbContextFactory<SaturnContext>
{
    public SaturnContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SaturnContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=saturn;Username=postgres;Password=mysecretpassword");
        return new SaturnContext(optionsBuilder.Options);
    }
}