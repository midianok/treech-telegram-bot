using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Saturn.Bot.Service.Extension;
using Saturn.Telegram.Db.Extensions;
using Saturn.Telegram.Infrastructure.Extension;
using Saturn.Telegram.Lib.Extensions;

var builder = Host.CreateApplicationBuilder();


builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);

builder.Services.AddTelegramBotClient<Program>(builder.Configuration);
builder.Services.AddMemoryCache();
var connectionString = builder.Configuration.GetSectionOrThrow("CONNECTION_STRING");
builder.Services.AddSaturnContext(connectionString);
var imageManipulationServiceUrl = builder.Configuration.GetSectionOrThrow("IMAGE_MANIPULATION_SERVICE_URL");
builder.Services.AddImageManipulationServiceClient(imageManipulationServiceUrl);

var app = builder.Build();
app.ApplyMigrations();

app.Run();
