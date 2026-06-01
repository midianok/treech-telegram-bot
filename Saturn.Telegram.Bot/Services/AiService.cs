using System.ClientModel;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Infrastructure.AtlasCloudImageClient;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Exceptions;

namespace Saturn.Bot.Service.Services;

public class AiService : IAiService
{
    private readonly ChatClient _chatClient;
    private readonly ChatClient _visionChatClient;
    private readonly AtlasCloudImageClient _atlasCloudImageClient;
    private readonly ILogger<AiService> _logger;

    public AiService(
        [FromKeyedServices("chat")] ChatClient chatClient,
        [FromKeyedServices("vision")] ChatClient visionChatClient,
        AtlasCloudImageClient atlasCloudImageClient,
        ILogger<AiService> logger)
    {
        _chatClient = chatClient;
        _visionChatClient = visionChatClient;
        _atlasCloudImageClient = atlasCloudImageClient;
        _logger = logger;
    }

    private static bool HasImageContent(IList<ChatMessage> messages) =>
        messages.OfType<UserChatMessage>()
            .SelectMany(m => m.Content)
            .Any(p => p.Kind == ChatMessageContentPartKind.Image);

    private ChatClient SelectClient(IList<ChatMessage> messages) =>
        HasImageContent(messages) ? _visionChatClient : _chatClient;

    public async Task<string> CompleteChatAsync(IList<ChatMessage> messages, CancellationToken ct = default)
    {
        try
        {
            var result = await SelectClient(messages).CompleteChatAsync(messages, cancellationToken: ct);
            return result.Value.Content.FirstOrDefault()?.Text ?? throw new AiEmptyResponseException();
        }
        catch (ClientResultException ex) when (ex.Status == 400)
        {
            _logger.LogError("AtlasCloud content moderation rejection (400 Bad Request)");
            throw new AiContentModerationException();
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            _logger.LogError("AtlasCloud balance exhausted (429 Too Many Requests)");
            throw new AiBudgetExhaustedException();
        }
    }

    public async Task<string> CompleteChatAsync(
        IList<ChatMessage> messages,
        IReadOnlyList<IChatTool> tools,
        CancellationToken ct = default)
    {
        try
        {
            var options = new ChatCompletionOptions { MaxOutputTokenCount = 500 };
            foreach (var tool in tools)
            {
                options.Tools.Add(tool.Definition);
            }

            var messagesList = messages.ToList();

            while (true)
            {
                var result = await SelectClient(messagesList).CompleteChatAsync(messagesList, options, ct);
                var completion = result.Value;

                if (completion.FinishReason != ChatFinishReason.ToolCalls)
                {
                    return completion.Content.FirstOrDefault()?.Text ?? 
                           throw new AiEmptyResponseException();
                }
                
                messagesList.Add(new AssistantChatMessage(completion));

                foreach (var toolCall in completion.ToolCalls)
                {
                    var tool = tools.FirstOrDefault(t => t.FunctionName == toolCall.FunctionName);
                    var toolResult = tool != null
                        ? await tool.ExecuteAsync(toolCall.FunctionArguments.ToString(), ct)
                        : """{"error":"Unknown tool"}""";
                    messagesList.Add(new ToolChatMessage(toolCall.Id, toolResult));
                }
            }
        }
        catch (ClientResultException ex) when (ex.Status == 400)
        {
            _logger.LogError("AtlasCloud content moderation rejection (400 Bad Request)");
            throw new AiContentModerationException();
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            _logger.LogError("AtlasCloud balance exhausted (429 Too Many Requests)");
            throw new AiBudgetExhaustedException();
        }
    }

    public async Task<byte[]> GenerateImageAsync(string prompt)
    {
        try
        {
            return await _atlasCloudImageClient.GenerateImageAsync(prompt);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            _logger.LogError("AtlasCloud content moderation rejection (400 Bad Request)");
            throw new AiContentModerationException();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.LogError("AtlasCloud balance exhausted (429 Too Many Requests)");
            throw new AiBudgetExhaustedException();
        }
    }

    public async Task<byte[]> EditImageAsync(IReadOnlyList<byte[]> images, string prompt)
    {
        try
        {
            return await _atlasCloudImageClient.EditImageAsync(images, prompt);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            _logger.LogError("AtlasCloud content moderation rejection (400 Bad Request)");
            throw new AiContentModerationException();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.LogError("AtlasCloud balance exhausted (429 Too Many Requests)");
            throw new AiBudgetExhaustedException();
        }
    }

    public async Task<byte[]> GenerateVideoFromImageAsync(byte[] image, string? prompt, string aspectRatio, bool generateAudio = false, CancellationToken ct = default)
    {
        try
        {
            return await _atlasCloudImageClient.GenerateVideoFromImageAsync(image, prompt, aspectRatio, generateAudio, ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            _logger.LogError("AtlasCloud content moderation rejection (400 Bad Request)");
            throw new AiContentModerationException();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.LogError("AtlasCloud balance exhausted (429 Too Many Requests)");
            throw new AiBudgetExhaustedException();
        }
    }
}
