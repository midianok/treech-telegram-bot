using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace Saturn.Telegram.Lib.Logging;

public class TelegramLoggerProvider : ILoggerProvider
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly IOptions<TelegramLoggerOptions> _options;

    public TelegramLoggerProvider(TelegramBotClient telegramBotClient, IOptions<TelegramLoggerOptions> options)
    {
        _telegramBotClient = telegramBotClient;
        _options = options;
    }

    public void Dispose() { }

    public ILogger CreateLogger(string categoryName) => 
        new TelegramLogger(categoryName, _telegramBotClient, _options);
}