using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Saturn.Telegram.Db.Extensions;

public static class HostExtensions
{
    public static void ApplyMigrations<T>(this IHost app) where T : DbContext
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<T>>()
            .CreateDbContext();
       
        dbContext.Database.Migrate();
    }
}