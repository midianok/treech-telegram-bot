using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Saturn.Telegram.Db.Extensions;

public static class ServiceCollectionsExtensions
{
    public static IServiceCollection AddSaturnContext(this IServiceCollection serviceCollection, string connectionString)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        
        return serviceCollection.AddDbContextFactory<SaturnContext>(options =>
        {
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }, ServiceLifetime.Transient);
    }
}