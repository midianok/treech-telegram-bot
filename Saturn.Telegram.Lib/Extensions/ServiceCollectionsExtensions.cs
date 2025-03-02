using Microsoft.Extensions.DependencyInjection;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;

namespace Saturn.Telegram.Lib.Extensions;

public static class ServiceCollectionsExtensions
{
    public static IServiceCollection AddTelegramBotClient<T>(this IServiceCollection serviceCollection, string botToken)
    {
        if (string.IsNullOrWhiteSpace(botToken))
        {
            throw new Exception("BOT_TOKEN is required");
        }
        
        serviceCollection.AddSingleton<TelegramBotClient>(_ => new TelegramBotClient(botToken));
        
        var operations = typeof(T).Assembly.GetTypes()
            .Where(x => x is { IsAbstract: false, IsClass: true } && x.GetInterface(nameof(IOperation)) == typeof(IOperation)).ToList();
        foreach (var rule in operations)
        {
            serviceCollection.Add(new ServiceDescriptor(rule, rule, ServiceLifetime.Singleton));
        }
        serviceCollection.AddSingleton<IEnumerable<IOperation>>(serviceProvider => operations.Select(serviceProvider.GetRequiredService).Cast<IOperation>()); 
        serviceCollection.AddHostedService<HostedService>();
        return serviceCollection;
    }
}