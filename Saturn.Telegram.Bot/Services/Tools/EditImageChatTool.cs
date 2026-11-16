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

/// <summary>Edits the pictures of the request during an AI chat conversation and posts the result. Created per request.</summary>
public class EditImageChatTool : IChatTool
{
    private static readonly GenerationProgressOptions ProgressOptions = new(
        Title: "🖌 Редактирую картинку",
        ProcessingStage: "редактирую",
        ChatAction: ChatAction.UploadPhoto,
        ExpectedDuration: TimeSpan.FromSeconds(30),
        FailureText: "не смог отредактировать, попробуй ещё раз");

    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IAiService _aiService;
    private readonly IGenerationProgressService _generationProgressService;
    private readonly ImageEditQuota _quota;
    private readonly ChatToolContext _context;
    private readonly ILogger _logger;
    private bool _imageSent;

    public EditImageChatTool(
        ITelegramBotClient telegramBotClient,
        IAiService aiService,
        IGenerationProgressService generationProgressService,
        ImageEditQuota quota,
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

    public string FunctionName => "edit_image";

    public ChatTool Definition => ToolDefinition;

    private static readonly ChatTool ToolDefinition = PromptLibrary.Tool("edit_image", new
    {
        type = "object",
        properties = new
        {
            prompt = new { type = "string" }
        },
        required = new[] { "prompt" }
    });

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
            return ToolArguments.Error("Опиши, что изменить");
        }

        // The model tends to soften or lose details when retelling the request, so the edit model also gets the original wording
        prompt = PromptLibrary.Get("Media/EditImage", new { prompt, request = _context.RequestText });

        var request = _context.Request;
        var user = request.From!;
        var charge = await _quota.TryChargeAsync(user, nameof(EditImageChatTool), сancellationToken);
        if (charge == ImageEditCharge.Denied)
        {
            var balance = await _quota.GetBalanceAsync(user, сancellationToken);
            return ToolArguments.Result(new
            {
                ok = false,
                limitReached = true,
                note =
                    $"Картинку НЕ отредактировал: бесплатное редактирование на этот час у собеседника уже использовано, " +
                    $"а платное стоит {_quota.Cost} трич койнов, у него на балансе {balance}. " +
                    "Ответь ему своими словами, что редактировать пока не будешь: можно подождать час или пополнить баланс командой «баланс». " +
                    "Не делай вид, что картинка будет"
            });
        }

        try
        {
            _logger.LogInformation("Editing {Count} images for user {UserId} in chat {ChatId} ({Charge}): {Prompt}",
                _context.Images.Count, user.Id, request.Chat.Id, charge, prompt);
            await _generationProgressService.RunAsync(
                request,
                ProgressOptions,
                onStatusChanged => _aiService.EditImageAsync(_context.Images, prompt, onStatusChanged),
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
            await _quota.RefundAsync(user, charge, nameof(EditImageChatTool), CancellationToken.None);
            _logger.LogWarning(ex, "Image editing from the AI chat failed");
            return ex switch
            {
                AiContentModerationException => ToolArguments.Error(
                    "Картинка не прошла модерацию, так отредактировать нельзя"),
                AiBudgetExhaustedException => ToolArguments.Error("Деньги на генерацию кончились"),
                _ => ToolArguments.Error("Не получилось отредактировать, собеседник уже видит сообщение об ошибке")
            };
        }

        _imageSent = true;
        _context.MarkActionTaken();
        return ToolArguments.Result(new { ok = true, note = "Отредактированная картинка отправлена в чат" });
    }
}