using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Saturn.Bot.Service.Extension;
using Saturn.Telegram.Db.Extensions;
using Saturn.Telegram.Infrastructure.Extension;
using Saturn.Telegram.Lib.Extensions;

var builder = Host.CreateApplicationBuilder();

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
builder.Services.AddMemoryCache();

var botToken = builder.Configuration.GetSectionOrThrow("BOT_TOKEN");
builder.Services.AddTelegramBotClient<Program>(botToken);
var connectionString = builder.Configuration.GetSectionOrThrow("CONNECTION_STRING");
builder.Services.AddSaturnContext(connectionString);

var imageManipulationServiceUrl = builder.Configuration.GetSectionOrThrow("IMAGE_MANIPULATION_SERVICE_URL");
builder.Services.AddImageManipulationServiceClient(imageManipulationServiceUrl);

var app = builder.Build();
app.ApplyMigrations();

app.Run();

return;


