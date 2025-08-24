using Microsoft.Extensions.DependencyInjection;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;

namespace Saturn.Telegram.Lib.Extensions;

public static class ServiceCollectionsExtensions
{
    public static IServiceCollection AddTelegramBotClient<T>(this IServiceCollection serviceCollection, string botToken)
    {
        var operations = typeof(T).Assembly.GetTypes()
            .Where(x => 
                x is { IsAbstract: false, IsClass: true } && 
                x.IsSubclassOf(typeof(OperationBase)))
            .ToList();
        
        foreach (var rule in operations)
        {
            serviceCollection.Add(new ServiceDescriptor(rule, rule, ServiceLifetime.Singleton));
        }
        
        serviceCollection.AddSingleton<IEnumerable<OperationBase>>(serviceProvider => operations.Select(serviceProvider.GetRequiredService).Cast<OperationBase>());
        serviceCollection.AddSingleton<TelegramBotClient>(_ => new TelegramBotClient(botToken));
        
        serviceCollection.AddHostedService<TelegramHostedService>();
        return serviceCollection;
    }
}