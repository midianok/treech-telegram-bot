using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Saturn.Bot.Service.Infrastructure.TelegramWidgetClient;

/// <summary>
/// Bot API 10.4 widget methods (<c>sendWidget</c>), which Telegram.Bot does not support yet.
/// The widget is sent as inline HTML in the <c>html</c> field of a JSON request: no multipart upload.
/// The server refuses HTML over 32768 bytes or over 4096 bytes after zlib level 9 compression.
/// See https://github.com/TelegramMessenger/widget-sandbox/blob/main/docs/widget-runtime-profile.md
/// </summary>
public class TelegramWidgetClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;

    public TelegramWidgetClient(HttpClient httpClient) => _httpClient = httpClient;

    public Task SendWidgetAsync(SendWidgetRequest request, CancellationToken cancellationToken) =>
        CallAsync("sendWidget", request, cancellationToken);

    private async Task CallAsync(string method, object request, CancellationToken cancellationToken)
    {
        // Buffered body (with Content-Length) rather than PostAsJsonAsync's chunked stream.
        using var content = new StringContent(JsonSerializer.Serialize(request, request.GetType(), JsonOptions), Encoding.UTF8, "application/json");

        // Relative URI: BaseAddress is ".../bot<token>/", so the token never appears in exception messages.
        using var response = await _httpClient.PostAsync(method, content, cancellationToken);

        BotApiResponse? body;
        try
        {
            body = await response.Content.ReadFromJsonAsync<BotApiResponse>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            body = null;
        }

        if (body is not { Ok: true })
        {
            throw new TelegramWidgetException(
                method,
                body?.ErrorCode ?? (int)response.StatusCode,
                body?.Description ?? response.ReasonPhrase ?? "unknown error");
        }
    }

    private sealed class BotApiResponse
    {
        public bool Ok { get; set; }

        public int? ErrorCode { get; set; }

        public string? Description { get; set; }
    }
}

public sealed class SendWidgetRequest
{
    public required long ChatId { get; init; }

    /// <summary>The widget's HTML, at most 32 KB and 4 KB compressed.</summary>
    public required string Html { get; init; }

    /// <summary>Bubble width; 0 or null means auto.</summary>
    public int? Width { get; init; }

    /// <summary>Bubble height; 0 or null means auto (content height, clamped to 50–600).</summary>
    public int? Height { get; init; }

    /// <summary>Shown in previews, the chat list and on clients that can't render widgets.</summary>
    public string? Caption { get; init; }

    public int? MessageThreadId { get; init; }

    public ReplyParametersDto? ReplyParameters { get; init; }
}

public sealed class ReplyParametersDto
{
    public required int MessageId { get; init; }

    public bool? AllowSendingWithoutReply { get; init; }
}

public sealed class TelegramWidgetException(string method, int errorCode, string description)
    : Exception($"{method} failed: {errorCode} {description}")
{
    public int ErrorCode { get; } = errorCode;

    public string Description { get; } = description;
}
