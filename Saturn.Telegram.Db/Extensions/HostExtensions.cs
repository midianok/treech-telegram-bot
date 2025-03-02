using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Saturn.Telegram.Db.Extensions;

public static class HostExtensions
{
    public static void ApplyMigrations(this IHost app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<SaturnContext>>()
            .CreateDbContext();
       
        var pendingMigrations = dbContext.Database.GetPendingMigrations();
        if (pendingMigrations.Any())
        {
            dbContext.Database.Migrate();
        }
    }
}