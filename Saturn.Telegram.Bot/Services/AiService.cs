using System.ClientModel;
using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Infrastructure.AtlasCloudImageClient;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Exceptions;

namespace Saturn.Bot.Service.Services;

public class AiService : IAiService
{
    private const int MaxToolIterations = 10;
    // Long answers (roasts, summaries of a person) need well over 500 tokens, or they get cut mid-sentence
    private const int MaxOutputTokens = 2000;
    private const int MaxLoggedMessageLength = 300;
    private const int MaxLoggedToolResultLength = 1500;

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

    private static bool HasImageContent(IList<ChatMessage> messages) =>
        messages.OfType<UserChatMessage>()
            .SelectMany(m => m.Content)
            .Any(p => p.Kind == ChatMessageContentPartKind.Image);

    private static ChatCompletionOptions CreateOptions()
    {
        // grok-4.7 always reasons; low effort roughly halves output tokens with no visible loss in answer quality
        // and leaves more of the output budget for the answer itself
#pragma warning disable OPENAI001
        return new ChatCompletionOptions { ReasoningEffortLevel = ChatReasoningEffortLevel.Low };
#pragma warning restore OPENAI001
    }

    public async Task<string> CompleteChatAsync(IList<ChatMessage> messages, CancellationToken сancellationToken)
    {
        try
        {
            LogRequest(messages, 0);
            var stopwatch = Stopwatch.StartNew();
            var result = await _chatClient.CompleteChatAsync(messages, CreateOptions(), сancellationToken);
            LogCompletion(result.Value, 0, stopwatch.Elapsed);
            return result.Value.Content.FirstOrDefault()?.Text ?? throw new AiEmptyResponseException();
        }
        catch (ClientResultException ex) when (ex.Status == 400)
        {
            _logger.LogError(ex, "AtlasCloud rejected the chat request (400 Bad Request)");
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
        CancellationToken сancellationToken)
    {
        try
        {
            var options = CreateOptions();
            options.MaxOutputTokenCount = MaxOutputTokens;
            foreach (var tool in tools)
            {
                options.Tools.Add(tool.Definition);
            }

            var messagesList = messages.ToList();
            LogRequest(messagesList, tools.Count);

            for (var iteration = 0; ; iteration++)
            {
                // After the limit, forbid further tool calls so the model answers from what it has gathered
                var toolLimitReached = iteration >= MaxToolIterations;
                if (toolLimitReached)
                {
                    _logger.LogWarning("Tool call limit of {Limit} iterations reached, forcing final answer", MaxToolIterations);
                    options.ToolChoice = ChatToolChoice.CreateNoneChoice();
                }

                var stopwatch = Stopwatch.StartNew();
                var result = await _chatClient.CompleteChatAsync(messagesList, options, сancellationToken);
                var completion = result.Value;
                LogCompletion(completion, iteration + 1, stopwatch.Elapsed);

                if (completion.FinishReason != ChatFinishReason.ToolCalls || toolLimitReached)
                {
                    if (completion.FinishReason == ChatFinishReason.Length)
                    {
                        _logger.LogWarning("AI answer cut off by the output limit of {Limit} tokens", MaxOutputTokens);
                    }

                    return completion.Content.FirstOrDefault()?.Text ?? 
                           throw new AiEmptyResponseException();
                }
                
                messagesList.Add(new AssistantChatMessage(completion));

                foreach (var toolCall in completion.ToolCalls)
                {
                    _logger.LogInformation("Tool call {ToolName} (iteration {Iteration}) with arguments {Arguments}",
                        toolCall.FunctionName, iteration + 1, toolCall.FunctionArguments.ToString());

                    var toolResult = await ExecuteToolAsync(tools, toolCall, сancellationToken);
                    messagesList.Add(new ToolChatMessage(toolCall.Id, toolResult));
                }
            }
        }
        catch (ClientResultException ex) when (ex.Status == 400)
        {
            _logger.LogError(ex, "AtlasCloud rejected the chat request (400 Bad Request)");
            throw new AiContentModerationException();
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            _logger.LogError("AtlasCloud balance exhausted (429 Too Many Requests)");
            throw new AiBudgetExhaustedException();
        }
    }

    public async Task<byte[]> GenerateImageAsync(string prompt, Action<string>? onStatusChanged)
    {
        try
        {
            return await _atlasCloudImageClient.GenerateImageAsync(prompt, onStatusChanged);
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

    public async Task<byte[]> EditImageAsync(IReadOnlyList<byte[]> images, string prompt, Action<string>? onStatusChanged)
    {
        try
        {
            return await _atlasCloudImageClient.EditImageAsync(images, prompt, onStatusChanged);
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

    public async Task<byte[]> GenerateVideoFromImageAsync(byte[] image, string? prompt, string aspectRatio, bool generateAudio, Action<string>? onStatusChanged, CancellationToken сancellationToken)
    {
        try
        {
            return await _atlasCloudImageClient.GenerateVideoFromImageAsync(image, prompt, aspectRatio, generateAudio, onStatusChanged, сancellationToken);
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

    private async Task<string> ExecuteToolAsync(IReadOnlyList<IChatTool> tools, ChatToolCall toolCall, CancellationToken сancellationToken)
    {
        var tool = tools.FirstOrDefault(t => t.FunctionName == toolCall.FunctionName);
        if (tool == null)
        {
            _logger.LogWarning("Model called unknown tool {ToolName}", toolCall.FunctionName);
            return """{"error":"Unknown tool"}""";
        }

        var stopwatch = Stopwatch.StartNew();
        string toolResult;
        try
        {
            toolResult = await tool.ExecuteAsync(toolCall.FunctionArguments.ToString(), сancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A broken tool shouldn't kill the whole answer: report it and let the model answer without it
            _logger.LogError(ex, "Tool {ToolName} failed with arguments {Arguments}", toolCall.FunctionName, toolCall.FunctionArguments.ToString());
            toolResult = """{"error":"Инструмент временно недоступен"}""";
        }

        _logger.LogInformation("Tool {ToolName} returned {Length} chars in {Elapsed:F1} s: {Result}",
            toolCall.FunctionName, toolResult.Length, stopwatch.Elapsed.TotalSeconds, Truncate(toolResult, MaxLoggedToolResultLength));
        return toolResult;
    }

    private void LogRequest(IList<ChatMessage> messages, int toolCount)
    {
        if (!_logger.IsEnabled(LogLevel.Information))
            return;

        _logger.LogInformation("AI request: {Count} messages, {ToolCount} tools, image {HasImage}",
            messages.Count, toolCount, HasImageContent(messages));
        for (var i = 0; i < messages.Count; i++)
        {
            _logger.LogInformation("AI request message {Index}/{Count} [{Role}]: {Text}",
                i + 1, messages.Count, GetRole(messages[i]), Truncate(GetText(messages[i]), MaxLoggedMessageLength));
        }
    }

    private void LogCompletion(ChatCompletion completion, int iteration, TimeSpan elapsed)
    {
        var toolCalls = completion.ToolCalls.Count > 0
            ? string.Join(", ", completion.ToolCalls.Select(c => c.FunctionName))
            : "none";
        _logger.LogInformation(
            "AI response (iteration {Iteration}) in {Elapsed:F1} s: finish {FinishReason}, tokens in {InputTokens} / out {OutputTokens}, tool calls: {ToolCalls}, text: {Text}",
            iteration, elapsed.TotalSeconds, completion.FinishReason, completion.Usage?.InputTokenCount, completion.Usage?.OutputTokenCount,
            toolCalls, Truncate(completion.Content.FirstOrDefault()?.Text, MaxLoggedMessageLength));
    }

    private static string GetRole(ChatMessage message) => message switch
    {
        SystemChatMessage => "system",
        UserChatMessage => "user",
        AssistantChatMessage => "assistant",
        ToolChatMessage => "tool",
        _ => message.GetType().Name
    };

    private static string GetText(ChatMessage message)
    {
        var text = string.Join(" ", message.Content.Select(p => p.Kind == ChatMessageContentPartKind.Image ? "<image>" : p.Text));
        if (message is AssistantChatMessage { ToolCalls.Count: > 0 } assistant)
        {
            text += " -> " + string.Join(", ", assistant.ToolCalls.Select(c => $"{c.FunctionName}({c.FunctionArguments})"));
        }
        return text;
    }

    private static string Truncate(string? text, int maxLength) =>
        text == null ? string.Empty
        : text.Length <= maxLength ? text
        : $"{text[..maxLength]}… (+{text.Length - maxLength} chars)";
}
