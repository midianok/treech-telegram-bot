using Saturn.Bot.Service.Services;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Operation;
using Saturn.Telegram.Lib.Attributes;
using Saturn.Telegram.Lib.Infrastructure.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

[ChatOnly("иди общайся в чат, хитрый пидарас")]
public class ImageEditOperation : IOperation
{
    private const string CommandPrefix1 = "отредактируй";
    private const string CommandPrefix2 = "измени";
    private const int MaxImages = 3;

    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IAiService _aiService;
    private readonly ISaveMessageService _saveMessageService;
    private readonly ImageEditQuota _imageEditQuota;
    private readonly ISparkShop _sparkShop;
    private readonly IGenerationProgressService _generationProgressService;

    private static readonly GenerationProgressOptions ProgressOptions = new(
        Title: "🖌 Редактирую картинку",
        ProcessingStage: "редактирую",
        ChatAction: ChatAction.UploadPhoto,
        ExpectedDuration: TimeSpan.FromSeconds(30),
        FailureText: "не смог отредактировать, попробуй ещё раз");

    public ImageEditOperation(
        ITelegramBotClient telegramBotClient,
        IAiService aiService,
        ISaveMessageService saveMessageService,
        ImageEditQuota imageEditQuota,
        ISparkShop sparkShop,
        IGenerationProgressService generationProgressService)
    {
        _telegramBotClient = telegramBotClient;
        _aiService = aiService;
        _saveMessageService = saveMessageService;
        _imageEditQuota = imageEditQuota;
        _sparkShop = sparkShop;
        _generationProgressService = generationProgressService;
    }

    public bool Validate(Message msg, UpdateType type)
    {
        if (type != UpdateType.Message) return false;
        var text = msg.Text ?? msg.Caption;
        var hasPrefix = text?.StartsWith(CommandPrefix1, StringComparison.CurrentCultureIgnoreCase) == true ||
                        text?.StartsWith(CommandPrefix2, StringComparison.CurrentCultureIgnoreCase) == true;

        if (!hasPrefix) return false;

        if (msg.ReplyToMessage is { Type: MessageType.Photo, Photo: not null })
            return true;

        if (msg.Photo != null)
            return true;

        return false;
    }

    public async Task OnMessageAsync(Message msg, UpdateType type, CancellationToken сancellationToken)
    {
        if (msg.From == null) return;

        var text = msg.Text ?? msg.Caption;
        var prefix = text!.StartsWith(CommandPrefix1, StringComparison.CurrentCultureIgnoreCase) ? CommandPrefix1 : CommandPrefix2;
        var prompt = text[prefix.Length..].Trim();

        var images = await DownloadImagesAsync(msg, сancellationToken);
        await EditAsync(msg, images, prompt, сancellationToken);
    }
    
    public async Task EditAsync(Message msg, IReadOnlyList<byte[]> images, string prompt, CancellationToken сancellationToken)
    {
        if (msg.From == null) return;

        var charge = await _imageEditQuota.TryChargeAsync(msg.From, nameof(ImageEditOperation), сancellationToken);
        if (charge == ImageEditCharge.Denied)
        {
            await _sparkShop.SendOfferAsync(msg, "Бесплатное редактирование на этот час использовано, а трич койнов не хватает.", сancellationToken);
            return;
        }

        try
        {
            await ProcessEditAsync(msg, images.Take(MaxImages).ToList(), prompt, сancellationToken);
        }
        catch
        {
            await _imageEditQuota.RefundAsync(msg.From, charge, nameof(ImageEditOperation), сancellationToken);
            throw;
        }
    }

    private async Task<IReadOnlyList<byte[]>> DownloadImagesAsync(Message msg, CancellationToken сancellationToken)
    {
        var images = new List<byte[]>();

        if (msg.ReplyToMessage?.Photo != null)
        {
            var fileId = msg.ReplyToMessage.Photo.MaxBy(x => x.FileSize)!.FileId;
            images.Add(await _telegramBotClient.DownloadFileAsync(fileId, сancellationToken));
        }

        if (msg.Photo != null)
        {
            var fileId = msg.Photo.MaxBy(x => x.FileSize)!.FileId;
            images.Add(await _telegramBotClient.DownloadFileAsync(fileId, сancellationToken));
        }

        return images;
    }

    private Task ProcessEditAsync(Message msg, IReadOnlyList<byte[]> images, string prompt, CancellationToken сancellationToken) =>
        _generationProgressService.RunAsync(
            msg,
            ProgressOptions,
            onStatusChanged => _aiService.EditImageAsync(images, prompt, onStatusChanged),
            async resultBytes =>
            {
                using var resultStream = new MemoryStream(resultBytes);
                var reply = await _telegramBotClient.SendPhoto(
                    msg.Chat.Id,
                    new InputFileStream(resultStream),
                    replyParameters: new ReplyParameters { MessageId = msg.MessageId }, cancellationToken: сancellationToken);

                await _saveMessageService.SaveMessageAsync(reply);
            },
            сancellationToken);
}
