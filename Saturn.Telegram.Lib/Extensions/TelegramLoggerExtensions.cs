using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Saturn.Telegram.Lib.Logging;

namespace Saturn.Telegram.Lib.Extensions;

public static class TelegramLoggerExtensions
{
    public static ILoggingBuilder AddTelegramLogger(this ILoggingBuilder builder, Action<TelegramLoggerOptions> configure)
    {
        builder.Services.Configure(configure);
        builder.Services.AddSingleton<ILoggerProvider, TelegramLoggerProvider>();
        return builder;
    }
}