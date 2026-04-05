using HttpClients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Saturn.Bot.Service.Infrastructure.XaiChatClient;
using Saturn.Bot.Service.Infrastructure.XaiImageEditClient;
using Saturn.Bot.Service.Infrastructure.XaiImageGenerationClient;
using Saturn.Bot.Service.Services;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db.Repositories;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Saturn.Telegram.Lib;
using Telegram.Bot;

namespace Saturn.Bot.Service.Extensions;

public static class ServiceCollectionsExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection serviceCollection, ConfigurationManager configuration)
    {
        serviceCollection.AddSingleton<TelegramBotClient>(_ =>
        {
            var botToken = configuration.GetSectionOrThrow("BOT_TOKEN");
            return new TelegramBotClient(botToken);
        });

        serviceCollection.AddHttpClient<XaiChatClient>(x =>
        {
            var apiKey = configuration.GetSectionOrThrow("CHAT_GENERATION_API_KEY");
            x.BaseAddress = new Uri("https://api.x.ai/");
            x.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            x.Timeout = TimeSpan.FromMinutes(5);
        });

        serviceCollection.AddHttpClient<XaiImageGenerationClient>(x =>
        {
            var apiKey = configuration.GetSectionOrThrow("IMAGE_GENERATION_API_KEY");
            x.BaseAddress = new Uri("https://api.x.ai/");
            x.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            x.Timeout = TimeSpan.FromMinutes(5);
        });

        serviceCollection.AddHttpClient<XaiImageEditClient>(x =>
        {
            var apiKey = configuration.GetSectionOrThrow("IMAGE_GENERATION_API_KEY");
            x.BaseAddress = new Uri("https://api.x.ai/");
            x.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            x.Timeout = TimeSpan.FromMinutes(5);
        });

        serviceCollection.AddHttpClient<IImageManipulationServiceClient, ImageManipulationServiceClient>(x =>
        {
            var imageManipulationServiceUrl = configuration.GetSectionOrThrow("IMAGE_MANIPULATION_SERVICE_URL");
            x.BaseAddress = new Uri(imageManipulationServiceUrl);
            x.Timeout = TimeSpan.FromMinutes(5);
        });

        serviceCollection
            .AddSingleton<IChatCachedRepository, ChatCachedRepository>()
            .AddSingleton<IMessageRepository, MessageRepository>()
            .AddSingleton<ISaveMessageService, SaveMessageService>()
            .AddSingleton<OperationManager>()
            .AddMemoryCache();

        return serviceCollection;
    }
}
