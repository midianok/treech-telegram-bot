using HttpClients;
using Microsoft.Extensions.DependencyInjection;

namespace Saturn.Telegram.Infrastructure.Extension;

public static class ServiceCollectionsExtensions
{
    public static IServiceCollection AddImageManipulationServiceClient(this IServiceCollection serviceCollection, string connectionString)
    {
        serviceCollection.AddHttpClient<IImageManipulationServiceClient, ImageManipulationServiceClient>(x =>
        {
            x.BaseAddress = new Uri(connectionString);
            x.Timeout = TimeSpan.FromMinutes(5);
        });

        return serviceCollection;
    }
}