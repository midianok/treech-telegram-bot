using HttpClients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Saturn.Bot.Service;
using Saturn.Bot.Service.Database;
using Saturn.Bot.Service.Extension;
using Saturn.Bot.Service.Operations.Abstractions;
using Saturn.Bot.Service.Options;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder();
builder.Services.AddOptions<OperationOptions>()
    .BindConfiguration(nameof(OperationOptions))
    .ValidateDataAnnotations();

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

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
    x.Timeout = TimeSpan.FromMinutes(5);
});

var operations = typeof(Program).Assembly.GetTypes()
    .Where(x => x is { IsAbstract: false, IsClass: true } && x.GetInterface(nameof(IOperation)) == typeof(IOperation)).ToList();
foreach (var rule in operations)
{
    builder.Services.Add(new ServiceDescriptor(rule, rule, ServiceLifetime.Singleton));
}
builder.Services.AddSingleton<IEnumerable<IOperation>>(serviceProvider => operations.Select(serviceProvider.GetRequiredService).Cast<IOperation>());
builder.Services.AddHostedService<HostedService>();

var app = builder.Build();
app.Run();