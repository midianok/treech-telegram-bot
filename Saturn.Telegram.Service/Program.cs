using HttpClients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Saturn.Bot.Service;
using Saturn.Bot.Service.Database;
using Saturn.Bot.Service.Extension;
using Saturn.Bot.Service.Operations;
using Saturn.Bot.Service.Operations.Abstractions;
using Saturn.Bot.Service.Options;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder();
builder.Services.AddOptions<OperationOptions>()
    .BindConfiguration(nameof(OperationOptions))
    .ValidateDataAnnotations();

builder.Services.AddDbContextFactory<SaturnContext>(options =>
{
    var connectionString = builder.Configuration.GetSectionOrThrow("CONNECTION_STRING");
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
}, ServiceLifetime.Transient);

builder.Services.AddSingleton<TelegramBotClient>(_ =>
{
    var botToken = builder.Configuration.GetSectionOrThrow("BOT_TOKEN");
    return new TelegramBotClient(botToken);
});

builder.Services.AddHttpClient<IImageManipulationServiceClient, ImageManipulationServiceClient>(x =>
{
    var imageManipulationServiceUrl = builder.Configuration.GetSectionOrThrow("IMAGE_MANIPULATION_SERVICE_URL");
    x.BaseAddress = new Uri(imageManipulationServiceUrl);
});


builder.Services.AddSingleton<CountOperation>();
builder.Services.AddSingleton<ShowChatLinkOperation>();
builder.Services.AddSingleton(GetEnabledOperations);

builder.Services.AddHostedService<HostedService>();
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var app = builder.Build();
app.Run();
return;

IEnumerable<IOperation> GetEnabledOperations(IServiceProvider services)
{
    var options = services.GetRequiredService<IOptions<OperationOptions>>().Value;
    if (options.CountOperationEnabled) yield return services.GetRequiredService<CountOperation>();
    if (options.ShowChatLinkOperationEnabled) yield return services.GetRequiredService<ShowChatLinkOperation>();
}