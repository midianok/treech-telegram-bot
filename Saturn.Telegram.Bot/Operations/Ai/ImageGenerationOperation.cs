using Saturn.Bot.Service.Extensions;
using Saturn.Bot.Service.Services;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Saturn.Telegram.Lib.Operation;
using Saturn.Telegram.Lib.Attributes;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

[GlobalCooldown(2)]
[ChatOnly("иди общайся в чат, хитрый пидарас")]
public class ImageGenerationOperation : IOperation
{
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IAiService _aiService;
    private readonly IImagePromptRepository _imagePromptRepository;
    private readonly IGenerationProgressService _generationProgressService;
    private readonly ImageGenerationQuota _imageGenerationQuota;

    private static readonly GenerationProgressOptions ProgressOptions = new(
        Title: "🎨 Рисую картинку",
        ProcessingStage: "рисую",
        ChatAction: ChatAction.UploadPhoto,
        ExpectedDuration: TimeSpan.FromSeconds(20),
        FailureText: "не смог нарисовать, попробуй ещё раз");

    public ImageGenerationOperation(
        ITelegramBotClient telegramBotClient,
        IAiService aiService,
        IImagePromptRepository imagePromptRepository,
        IGenerationProgressService generationProgressService,
        ImageGenerationQuota imageGenerationQuota)
    {
        _telegramBotClient = telegramBotClient;
        _aiService = aiService;
        _imagePromptRepository = imagePromptRepository;
        _generationProgressService = generationProgressService;
        _imageGenerationQuota = imageGenerationQuota;
    }

    public bool Validate(Message msg, UpdateType type) =>
        msg.TextStartsWith("покажи");

    public async Task OnMessageAsync(Message msg, UpdateType type, CancellationToken сancellationToken)
    {
        if (msg.From == null) return;

        if (!_imageGenerationQuota.TryConsume(msg.From, out var wait))
        {
            await _telegramBotClient.SendMessage(msg.Chat, $"Слишком часто. Следующий раз можно через {Math.Ceiling(wait.TotalMinutes):0} мин.",
                replyParameters: new ReplyParameters { MessageId = msg.Id }, cancellationToken: сancellationToken);
            return;
        }

        try
        {
            var rawQuery = msg.Text!.ToLower().Replace("покажи", string.Empty).Trim();
            var prompt = await _imagePromptRepository.FindPromptAsync(rawQuery) ?? rawQuery;

            await _generationProgressService.RunAsync(
                msg,
                ProgressOptions,
                onStatusChanged => _aiService.GenerateImageAsync(prompt, onStatusChanged),
                async result =>
                {
                    using var generatedStream = new MemoryStream(result);
                    await _telegramBotClient.SendPhoto(msg.Chat.Id, new InputFileStream(generatedStream), replyParameters: new ReplyParameters { MessageId = msg.MessageId });
                },
                сancellationToken);
        }
        catch
        {
            _imageGenerationQuota.Release(msg.From);
            throw;
        }
    }
}
