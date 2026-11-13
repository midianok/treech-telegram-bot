using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Saturn.Telegram.Lib.Extensions;

public static class JsonConsoleLoggingExtensions
{
    /// <summary>
    /// Writes console logs as one JSON object per line: message template parameters become separate fields.
    /// Other providers (e.g. the Telegram logger) keep their own format.
    /// </summary>
    public static ILoggingBuilder AddJsonConsoleLogging(this ILoggingBuilder builder) =>
        builder.AddJsonConsole(options =>
        {
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            options.IncludeScopes = true;
            options.JsonWriterOptions = new JsonWriterOptions
            {
                // Keep Cyrillic readable instead of \uXXXX escapes
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
        });
}
