using Microsoft.Extensions.Logging;

namespace Saturn.Telegram.Lib.Logging;

public class TelegramLoggerOptions
{
    private readonly Dictionary<string, LogLevel> _filters = new();
    public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;
    
    public long TelegramLoggingChatId { get; set; }

    public void AddFilter(string pattern, LogLevel level) => 
        _filters.Add(pattern, level);

    public LogLevel? TryGetFilter(string pattern)
    {
        if (_filters.TryGetValue(pattern, out var result))
        {
            return result;
        }
        return null;
    } 
        
}