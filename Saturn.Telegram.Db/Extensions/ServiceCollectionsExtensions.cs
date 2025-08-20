using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Saturn.Telegram.Db.Extensions;

public static class ServiceCollectionsExtensions
{
    public static IServiceCollection AddSaturnContext(this IServiceCollection serviceCollection, ConfigurationManager configuration)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        
        var connectionString = configuration.GetSection("CONNECTION_STRING").Value;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new Exception($"Configuration item \"CONNECTION_STRING\" not presented");
        }
        
        return serviceCollection.AddDbContextFactory<SaturnContext>(options =>
        {
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
            options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        }, ServiceLifetime.Transient);
    }
}