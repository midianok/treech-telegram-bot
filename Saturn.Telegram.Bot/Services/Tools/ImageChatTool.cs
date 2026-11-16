using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Bot.Service.Services.Tools.Utils;
using Saturn.Telegram.Lib.Exceptions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Saturn.Bot.Service.Services.Prompts;

namespace Saturn.Bot.Service.Services.Tools;

/// <summary>Draws a picture during an AI chat conversation and posts it to the chat. Created per request.</summary>
public class ImageChatTool : IChatTool
{
    private static readonly GenerationProgressOptions ProgressOptions = new(
        Title: "🎨 Рисую картинку",
        ProcessingStage: "рисую",
        ChatAction: ChatAction.UploadPhoto,
        ExpectedDuration: TimeSpan.FromSeconds(20),
        FailureText: "не смог нарисовать, попробуй ещё раз");

    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IAiService _aiService;
    private readonly IGenerationProgressService _generationProgressService;
    private readonly ImageGenerationQuota _quota;
    private readonly ChatToolContext _context;
    private readonly ILogger _logger;
    private bool _imageSent;

    public ImageChatTool(
        ITelegramBotClient telegramBotClient,
        IAiService aiService,
        IGenerationProgressService generationProgressService,
        ImageGenerationQuota quota,
        ChatToolContext context,
        ILogger logger)
    {
        _telegramBotClient = telegramBotClient;
        _aiService = aiService;
        _generationProgressService = generationProgressService;
        _quota = quota;
        _context = context;
        _logger = logger;
    }

    public string FunctionName => "generate_image";

    public ChatTool Definition => ToolDefinition;

    private static readonly ChatTool ToolDefinition = PromptLibrary.Tool("generate_image", new
    {
        type = "object",
        properties = new
        {
            prompt = new { type = "string" }
        },
        required = new[] { "prompt" }
    }, new { periodMinutes = (int)ImageGenerationQuota.Period.TotalMinutes });

    public async Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken)
    {
        if (_imageSent)
        {
            return ToolArguments.Error("Одна картинка за ответ");
        }

        using var doc = JsonDocument.Parse(arguments);
        var prompt = ToolArguments.GetString(doc.RootElement, "prompt");
        if (prompt == null)
        {
            return ToolArguments.Error("Опиши, что нарисовать");
        }

        var request = _context.Request;
        var user = request.From!;
        if (!_quota.TryConsume(user, out var wait))
        {
            return ToolArguments.Result(new
            {
                ok = false,
                limitReached = true,
                note =
                    $"Картинку НЕ нарисовал: лимит — одна картинка на человека раз в {ImageGenerationQuota.Period.TotalMinutes:0} минут, " +
                    $"следующую этому собеседнику можно через {Math.Ceiling(wait.TotalMinutes):0} мин. " +
                    "Ответь ему своими словами, что пока рисовать не будешь и когда можно снова; не делай вид, что картинка будет"
            });
        }

        try
        {
            _logger.LogInformation("Generating image for user {UserId} in chat {ChatId}: {Prompt}", user.Id,
                request.Chat.Id, prompt);
            await _generationProgressService.RunAsync(
                request,
                ProgressOptions,
                onStatusChanged => _aiService.GenerateImageAsync(prompt, onStatusChanged),
                async result =>
                {
                    using var stream = new MemoryStream(result);
                    await _telegramBotClient.SendPhoto(request.Chat.Id, new InputFileStream(stream),
                        replyParameters: new ReplyParameters
                            { MessageId = request.Id, AllowSendingWithoutReply = true },
                        cancellationToken: сancellationToken);
                },
                сancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !сancellationToken.IsCancellationRequested)
        {
            _quota.Release(user);
            _logger.LogWarning(ex, "Image generation from the AI chat failed");
            return ex switch
            {
                AiContentModerationException => ToolArguments.Error(
                    "Картинка не прошла модерацию, нарисовать это нельзя"),
                AiBudgetExhaustedException => ToolArguments.Error("Деньги на генерацию кончились"),
                _ => ToolArguments.Error("Не получилось нарисовать, собеседник уже видит сообщение об ошибке")
            };
        }

        _imageSent = true;
        _context.MarkActionTaken();
        return ToolArguments.Result(new { ok = true, note = "Картинка отправлена в чат" });
    }
}