using System.ClientModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Images;
using Saturn.Telegram.Db.Repositories;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Saturn.Telegram.Lib.Operation;
using Saturn.Telegram.Lib.Services;
using Saturn.Telegram.Lib.Services.Abstractions;
using Telegram.Bot;

namespace Saturn.Telegram.Lib.Extensions;

public static class ServiceCollectionsExtensions
{
    public static IServiceCollection AddTelegramBotClient<T>(this IServiceCollection serviceCollection, ConfigurationManager configuration)
    {
        serviceCollection.AddSingleton<TelegramBotClient>(_ =>
        {
            var botToken = configuration.GetSectionOrThrow("BOT_TOKEN");
            return new TelegramBotClient(botToken);
        });
        
        serviceCollection.AddSingleton<ChatClient>(_ =>
        {
            var apiKey = configuration.GetSectionOrThrow("CHAT_GENERATION_API_KEY");
            return new ChatClient("grok-3-mini-fast", new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri("https://api.x.ai/v1") });
        });
        
        serviceCollection.AddSingleton<ImageClient>(_ =>
        {
            var apiKey = configuration.GetSectionOrThrow("IMAGE_GENERATION_API_KEY");
            return new ImageClient("dall-e-3", apiKey);
        });
            
        serviceCollection.AddSingleton<ICooldownService, CooldownService>(); 
        serviceCollection.AddSingleton<IChatCachedRepository, ChatCachedRepository>(); 
        serviceCollection.AddSingleton<IMessageRepository, MessageRepository>(); 
        serviceCollection.AddSingleton<ISaveMessageService, SaveMessageService>(); 
        serviceCollection.AddSingleton<ISubscriptionService, SubscriptionService>(); 
        
        RegisterOperations<T>(serviceCollection);
        
        serviceCollection.AddHostedService<HostedService>();
        return serviceCollection;
    }

    private static void RegisterOperations<T>(IServiceCollection serviceCollection)
    {
        var operations = typeof(T).Assembly.GetTypes()
            .Where(x => 
                x is { IsAbstract: false, IsClass: true } && 
                x.GetInterface(nameof(IOperation)) == typeof(IOperation))
            .ToList();
        
        foreach (var rule in operations)
        {
            serviceCollection.Add(new ServiceDescriptor(rule, rule, ServiceLifetime.Singleton));
        }
        
        serviceCollection.AddSingleton<IEnumerable<IOperation>>(serviceProvider => operations.Select(serviceProvider.GetRequiredService).Cast<IOperation>());
    }
}