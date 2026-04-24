using System.ClientModel;
using System.Net;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using OpenAI.Images;
using Saturn.Bot.Service.Infrastructure.XaiImageEditClient;
using Saturn.Bot.Service.Infrastructure.XaiVideoGenerationClient;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Exceptions;

namespace Saturn.Bot.Service.Services;

public class AiService : IAiService
{
    private readonly ChatClient _chatClient;
    private readonly ImageClient _imageClient;
    private readonly XaiImageEditClient _xaiImageEditClient;
    private readonly XaiVideoGenerationClient _xaiVideoGenerationClient;
    private readonly ILogger<AiService> _logger;

    public AiService(
        ChatClient chatClient,
        ImageClient imageClient,
        XaiImageEditClient xaiImageEditClient,
        XaiVideoGenerationClient xaiVideoGenerationClient,
        ILogger<AiService> logger)
    {
        _chatClient = chatClient;
        _imageClient = imageClient;
        _xaiImageEditClient = xaiImageEditClient;
        _xaiVideoGenerationClient = xaiVideoGenerationClient;
        _logger = logger;
    }

    public async Task<string> CompleteChatAsync(IList<ChatMessage> messages, CancellationToken ct = default)
    {
        try
        {
            var result = await _chatClient.CompleteChatAsync(messages, cancellationToken: ct);
            return result.Value.Content.FirstOrDefault()?.Text ?? throw new AiEmptyResponseException();
        }
        catch (ClientResultException ex) when (ex.Status == 400)
        {
            _logger.LogError("xAI content moderation rejection (400 Bad Request)");
            throw new AiContentModerationException();
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            _logger.LogError("xAI balance exhausted (429 Too Many Requests)");
            throw new AiBudgetExhaustedException();
        }
    }

    public async Task<GeneratedImage> GenerateImageAsync(string prompt, ImageGenerationOptions? options = null)
    {
        try
        {
            var result = await _imageClient.GenerateImageAsync(prompt, options);
            return result.Value;
        }
        catch (ClientResultException ex) when (ex.Status == 400)
        {
            _logger.LogError("xAI content moderation rejection (400 Bad Request)");
            throw new AiContentModerationException();
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            _logger.LogError("xAI balance exhausted (429 Too Many Requests)");
            throw new AiBudgetExhaustedException();
        }
    }

    public async Task<byte[]> EditImageAsync(IReadOnlyList<byte[]> images, string prompt)
    {
        try
        {
            return await _xaiImageEditClient.EditImageAsync(images, prompt);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            _logger.LogError("xAI content moderation rejection (400 Bad Request)");
            throw new AiContentModerationException();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.LogError("xAI balance exhausted (429 Too Many Requests)");
            throw new AiBudgetExhaustedException();
        }
    }

    public async Task<byte[]> GenerateVideoFromImageAsync(byte[] image, CancellationToken ct = default)
    {
        try
        {
            return await _xaiVideoGenerationClient.GenerateVideoFromImageAsync(image, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            _logger.LogError("xAI content moderation rejection (400 Bad Request)");
            throw new AiContentModerationException();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.LogError("xAI balance exhausted (429 Too Many Requests)");
            throw new AiBudgetExhaustedException();
        }
    }
}
