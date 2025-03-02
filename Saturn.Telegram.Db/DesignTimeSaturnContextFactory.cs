using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Saturn.Telegram.Db;

public class DesignTimeSaturnContextFactory : IDesignTimeDbContextFactory<SaturnContext>
{
    public SaturnContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SaturnContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=saturn;Username=postgres;Password=mysecretpassword")
            .UseSnakeCaseNamingConvention();
        return new SaturnContext(optionsBuilder.Options);
    }
}