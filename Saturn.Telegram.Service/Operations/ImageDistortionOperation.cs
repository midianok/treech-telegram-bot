using HttpClients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Saturn.Bot.Service.Operations.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations;

public class ImageDistortionOperation : OperationBase
{
    private readonly IImageManipulationServiceClient _imageManipulationService;
    private readonly TelegramBotClient _telegramBotClient;
    private static readonly string[] triggerWords = ["жмыхни", "нука"];

    public ImageDistortionOperation(ILogger<IOperation> logger, IConfiguration configuration, IImageManipulationServiceClient imageManipulationService, TelegramBotClient telegramBotClient) : base(logger, configuration)
    {
        _imageManipulationService = imageManipulationService;
        _telegramBotClient = telegramBotClient;
    }

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var fileId = GetFileId(msg);
        if (string.IsNullOrEmpty(fileId))
        {
            return;
        }

        var file = await _telegramBotClient.GetFile(fileId);
        if (string.IsNullOrEmpty(file.FilePath))
        {
            return;
        }

        using var downloadFileStream = new MemoryStream();
        await _telegramBotClient.DownloadFile(file.FilePath, downloadFileStream);

        if (msg.ReplyToMessage!.Type == MessageType.Photo)
        {
            var result = await _imageManipulationService.DistortImageAsync(new DistortDto
            {
                Base64 = Convert.ToBase64String(downloadFileStream.ToArray())
            });

            using var sendFileStream = new MemoryStream(result.Base64);
            await _telegramBotClient.SendPhoto(msg.Chat.Id, new InputFileStream(sendFileStream), replyParameters: new ReplyParameters{MessageId = msg.MessageId});
        }
        else if (msg.ReplyToMessage!.Type == MessageType.Video || msg.ReplyToMessage!.Type == MessageType.Animation)
        {
            var result = await _imageManipulationService.DistortVideoAsync(new DistortDto
            {
                Base64 = Convert.ToBase64String(downloadFileStream.ToArray())
            });
            using var sendFileStream = new MemoryStream(result.Base64);
            await _telegramBotClient.SendVideo(msg.Chat.Id, new InputFileStream(sendFileStream), replyParameters: new ReplyParameters{MessageId = msg.MessageId});
        }
    }

    private string GetFileId(Message msg) =>
        msg.ReplyToMessage!.Type switch
        {
            MessageType.Photo => msg.ReplyToMessage!.Photo!.MaxBy(x => x.FileSize)!.FileId,
            MessageType.Video => msg.ReplyToMessage!.Video!.FileId,
            MessageType.Animation => msg.ReplyToMessage!.Animation!.FileId,
            _ => string.Empty
        };

    protected override bool ValidateOnMessage(Message msg, UpdateType type) =>
        type == UpdateType.Message &&
        (msg.ReplyToMessage?.Photo != null || msg.ReplyToMessage?.Video != null || msg.ReplyToMessage?.Animation != null) &&
        triggerWords.Contains(msg.Text?.ToLower());
}