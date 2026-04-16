using Microsoft.AspNetCore.HttpOverrides;
using Saturn.Telegram.Db.Extensions;

var builder = WebApplication.CreateBuilder(args);
var pathBase = builder.Configuration["PATH_BASE"];

// Add services to the container.

builder.Services.AddSaturnContext(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("https://routefabric.ru")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (!string.IsNullOrWhiteSpace(pathBase))
{
    if (!pathBase.StartsWith('/'))
    {
        pathBase = "/" + pathBase;
    }

    app.UsePathBase(pathBase);
}

app.UseSwagger(options =>
{
    options.RouteTemplate = "swagger/{documentName}/swagger.json";
});
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("v1/swagger.json", "Saturn API v1");
});
app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run("http://0.0.0.0:5001");
