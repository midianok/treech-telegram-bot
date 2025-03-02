using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Saturn.Telegram.Db.Extensions;

public static class ServiceCollectionsExtensions
{
    public static IServiceCollection AddSaturnContext(this IServiceCollection serviceCollection, string connectionString)
    {
       return serviceCollection.AddDbContextFactory<SaturnContext>(options =>
        {
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        }, ServiceLifetime.Transient);
    }
}