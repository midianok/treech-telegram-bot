using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Saturn.Bot.Service.Extensions;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Extensions;
using Saturn.Telegram.Lib.Extensions;

var builder = Host.CreateApplicationBuilder();

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.None);
var botToken = builder.Configuration.GetSectionOrThrow("BOT_TOKEN");
builder.Services
    .AddTelegramBotClient<Program>(botToken)
    .AddServices(builder.Configuration);

builder.Services.AddSaturnContext(builder.Configuration);

var app = builder.Build();

app.ApplyMigrations<SaturnContext>();
app.Run();
