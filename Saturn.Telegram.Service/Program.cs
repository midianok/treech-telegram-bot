using HttpClients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Saturn.Bot.Service;
using Saturn.Bot.Service.Database;
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
    var connectionString = builder.Configuration.GetSection("CONNECTION_STRING").Value;
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new Exception("Env variable CONNECTION_STRING not presented");
    }
    options.UseNpgsql(connectionString)
        .UseSnakeCaseNamingConvention();
}, ServiceLifetime.Transient);


builder.Services.AddSingleton<TelegramBotClient>(_ =>
{
    var botToken = builder.Configuration.GetSection("BOT_TOKEN").Value;
    if (string.IsNullOrWhiteSpace(botToken))
    {
        throw new Exception("Env variable BOT_TOKEN not presented");
    }
    return new TelegramBotClient(botToken);
});

builder.Services.AddHttpClient<IImageManipulationServiceClient, ImageManipulationServiceClient>(x =>
{
    var imageManipulationServiceUrl = builder.Configuration.GetSection("IMAGE_MANIPULATION_SERVICE_URL").Value;
    if (string.IsNullOrWhiteSpace(imageManipulationServiceUrl))
    {
        throw new Exception("Env variable ImageManipulationServiceUrl not presented");
    }
    x.BaseAddress = new Uri(imageManipulationServiceUrl);
});


builder.Services.AddSingleton<CountOperation>();
builder.Services.AddSingleton<ShowChatLinkOperation>();
builder.Services.AddSingleton(GetEnabledOperations);

builder.Services.AddHostedService<HostedService>();

var app = builder.Build();
app.Run();
return;

IEnumerable<IOperation> GetEnabledOperations(IServiceProvider services)
{
    var options = services.GetRequiredService<IOptions<OperationOptions>>().Value;
    if (options.CountOperationEnabled) yield return services.GetRequiredService<CountOperation>();
    if (options.ShowChatLinkOperationEnabled) yield return services.GetRequiredService<ShowChatLinkOperation>();
}