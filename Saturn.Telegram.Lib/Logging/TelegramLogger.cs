using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Lib.Logging;

public class TelegramLogger : ILogger
{
    private readonly TelegramLoggerOptions _telegramLoggerOptions;
    private readonly TelegramBotClient _telegramBotClient;
    private readonly string _categoryName;

    public TelegramLogger(string categoryName, TelegramBotClient telegramBotClient, IOptions<TelegramLoggerOptions> options)
    {
        _categoryName = categoryName;
        _telegramBotClient = telegramBotClient;
        _telegramLoggerOptions = options.Value;
    }

    public IDisposable BeginScope<TState>(TState state) => null!;

    public bool IsEnabled(LogLevel logLevel)
    {
        var filter = _telegramLoggerOptions.TryGetFilter(_categoryName);
        if (filter != null)
        {
            return logLevel >= filter;
        }
        return logLevel >=_telegramLoggerOptions.MinimumLevel;
    } 
        

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var logMessage = formatter(state, exception);
        var logLevelStr = GetLogLevelString(logLevel);
        var message = $"*[{DateTime.Now}]* *[{logLevelStr}]* [{_categoryName}]\n\n{logMessage}";
        _telegramBotClient.SendMessage(_telegramLoggerOptions.TelegramLoggingChatId, message , ParseMode.Markdown);
    }

    private string GetLogLevelString(LogLevel logLevel) =>
        logLevel switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARNING",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRITICAL",
            LogLevel.None => "NONE",
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null)
        };
}