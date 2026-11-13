using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Exceptions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

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
    private readonly ChatImageQuota _quota;
    private readonly ChatToolContext _context;
    private readonly bool _isAdmin;
    private readonly ILogger _logger;
    private bool _imageSent;

    public ImageChatTool(
        ITelegramBotClient telegramBotClient,
        IAiService aiService,
        IGenerationProgressService generationProgressService,
        ChatImageQuota quota,
        ChatToolContext context,
        bool isAdmin,
        ILogger logger)
    {
        _telegramBotClient = telegramBotClient;
        _aiService = aiService;
        _generationProgressService = generationProgressService;
        _quota = quota;
        _context = context;
        _isAdmin = isAdmin;
        _logger = logger;
    }

    public string FunctionName => "generate_image";

    public ChatTool Definition { get; } = ChatTool.CreateFunctionTool(
        functionName: "generate_image",
        functionDescription: "Нарисовать картинку и отправить её в чат. Используй, когда собеседник просит нарисовать, " +
                             "показать или изобразить что-то («нарисуй, как Дима это говорит», «покажи кота-бухгалтера»). " +
                             "Сам по своей инициативе не рисуй. Картинка уходит в чат сразу, текстом можешь коротко её прокомментировать. " +
                             $"Лимит — одна картинка на человека раз в {ChatImageQuota.Period.TotalMinutes:0} минут.",
        functionParameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                prompt = new
                {
                    type = "string",
                    description = "Подробное описание картинки на английском: сюжет, персонажи, обстановка, стиль. " +
                                  "Участников чата описывай словами (внешность ты не знаешь), без имён и ников"
                }
            },
            required = new[] { "prompt" }
        })
    );

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
        var userId = request.From!.Id;
        if (!_isAdmin && !_quota.TryConsume(userId, out var wait))
        {
            return ToolArguments.Error($"Лимит картинок: следующую этому собеседнику можно через {Math.Ceiling(wait.TotalMinutes):0} мин. Скажи ему об этом");
        }

        try
        {
            _logger.LogInformation("Generating image for user {UserId} in chat {ChatId}: {Prompt}", userId, request.Chat.Id, prompt);
            await _generationProgressService.RunAsync(
                request,
                ProgressOptions,
                onStatusChanged => _aiService.GenerateImageAsync(prompt, onStatusChanged),
                async result =>
                {
                    using var stream = new MemoryStream(result);
                    await _telegramBotClient.SendPhoto(request.Chat.Id, new InputFileStream(stream),
                        replyParameters: new ReplyParameters { MessageId = request.Id, AllowSendingWithoutReply = true },
                        cancellationToken: сancellationToken);
                },
                сancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !сancellationToken.IsCancellationRequested)
        {
            _quota.Release(userId);
            _logger.LogWarning(ex, "Image generation from the AI chat failed");
            return ex switch
            {
                AiContentModerationException => ToolArguments.Error("Картинка не прошла модерацию, нарисовать это нельзя"),
                AiBudgetExhaustedException => ToolArguments.Error("Деньги на генерацию кончились"),
                _ => ToolArguments.Error("Не получилось нарисовать, собеседник уже видит сообщение об ошибке")
            };
        }

        _imageSent = true;
        _context.MarkActionTaken();
        return ToolArguments.Result(new { ok = true, note = "Картинка отправлена в чат" });
    }
}
