using System.ClientModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using OpenAI.Chat;
using Saturn.Bot.Service.Infrastructure.AtlasCloudImageClient;
using Saturn.Bot.Service.Infrastructure.CurrencyClient;
using Saturn.Bot.Service.Infrastructure.WeatherClient;
using Saturn.Bot.Service.Options;
using Saturn.Bot.Service.Services;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Bot.Service.Services.Tools;
using Saturn.Telegram.Db.Repositories;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Saturn.Telegram.Lib;
using Saturn.Telegram.Lib.Infrastructure;
using Saturn.Telegram.Lib.Infrastructure.Abstractions;
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
        
        serviceCollection.AddSingleton<ChatClient>(_ =>
        {
            var apiKey = configuration.GetSectionOrThrow("ATLAS_CLOUD_API_KEY");
            return new ChatClient("qwen/qwen3-vl-8b-instruct", new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri("https://api.atlascloud.ai/v1") });
        });
        
        serviceCollection.AddHttpClient<AtlasCloudImageClient>(x =>
        {
            var apiKey = configuration.GetSectionOrThrow("ATLAS_CLOUD_API_KEY");
            x.BaseAddress = new Uri("https://api.atlascloud.ai/");
            x.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            x.Timeout = TimeSpan.FromMinutes(5);
        });

        serviceCollection.AddHttpClient<WeatherClient>(x =>
        {
            x.BaseAddress = new Uri("http://wttr.in/");
            x.Timeout = TimeSpan.FromSeconds(10);
        });

        serviceCollection.AddHttpClient<CurrencyClient>(x =>
        {
            x.BaseAddress = new Uri("https://open.er-api.com/");
            x.Timeout = TimeSpan.FromSeconds(10);
        });
        
        
        serviceCollection.Configure<BotOptions>(options =>
        {
            options.BotToken = configuration["BOT_TOKEN"] ?? string.Empty;
            options.BotUsername = configuration["BOT_USERNAME"] ?? string.Empty;
            options.InvokeCommand = configuration.GetSectionOrThrow("INVOKE_COMMAND");
            options.LogChatId = long.TryParse(configuration["LOG_CHAT_ID"], out var chatId) ? chatId : 0;
            options.AdminUsername = configuration["ADMIN_USERNAME"];
            options.EasterEggUsername = configuration["EASTER_EGG_USERNAME"];
        });

        serviceCollection.Configure<BotOptions>(options =>
        {
            
        });

        serviceCollection
            .AddSingleton<IChatTool, WeatherChatTool>()
            .AddSingleton<IChatTool, CurrencyChatTool>()
            .AddSingleton<IAiService, AiService>()
            .AddSingleton<IChatCachedRepository, ChatCachedRepository>()
            .AddSingleton<IMessageRepository, MessageRepository>()
            .AddSingleton<IOperationCallRepository, OperationCallRepository>()
            .AddSingleton<IImagePromptRepository, ImagePromptRepository>()
            .AddSingleton<IDistortionService, DistortionService>()
            .AddSingleton<ISaveMessageService, SaveMessageService>()
            .AddSingleton<OperationManager>()
            .AddHostedService<FfmpegSetupService>()
            .AddHostedService<YtDlpSetupService>()
            .AddHostedService<YtDlpUpdateService>()
            .AddHostedService<CacheInvalidationService>()
            .AddMemoryCache();
        
        return serviceCollection;
    }
}