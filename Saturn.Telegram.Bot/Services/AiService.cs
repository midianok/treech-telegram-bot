using System.ClientModel;
using System.Net;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Infrastructure.AtlasCloudImageClient;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Exceptions;

namespace Saturn.Bot.Service.Services;

public class AiService : IAiService
{
    private readonly ChatClient _chatClient;
    private readonly AtlasCloudImageClient _atlasCloudImageClient;
    private readonly ILogger<AiService> _logger;

    public AiService(
        ChatClient chatClient,
        AtlasCloudImageClient atlasCloudImageClient,
        ILogger<AiService> logger)
    {
        _chatClient = chatClient;
        _atlasCloudImageClient = atlasCloudImageClient;
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

    public async Task<string> CompleteChatAsync(
        IList<ChatMessage> messages,
        IReadOnlyList<IChatTool> tools,
        CancellationToken ct = default)
    {
        try
        {
            var options = new ChatCompletionOptions();
            foreach (var tool in tools)
            {
                options.Tools.Add(tool.Definition);
            }

            var messagesList = messages.ToList();

            while (true)
            {
                var result = await _chatClient.CompleteChatAsync(messagesList, options, ct);
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
            _logger.LogError("xAI content moderation rejection (400 Bad Request)");
            throw new AiContentModerationException();
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            _logger.LogError("xAI balance exhausted (429 Too Many Requests)");
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

    public async Task<byte[]> GenerateVideoFromImageAsync(byte[] image, string? prompt, string aspectRatio, CancellationToken ct = default)
    {
        try
        {
            return await _atlasCloudImageClient.GenerateVideoFromImageAsync(image, prompt, aspectRatio, ct);
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
