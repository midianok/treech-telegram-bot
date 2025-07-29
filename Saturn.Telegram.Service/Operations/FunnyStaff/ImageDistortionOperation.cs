using HttpClients;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.FunnyStaff;

public class ImageDistortionOperation : OperationBase
{
    private readonly IImageManipulationServiceClient _imageManipulationService;

    public ImageDistortionOperation(IImageManipulationServiceClient imageManipulationService)
    {
        _imageManipulationService = imageManipulationService;
    }

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var fileId = GetFileId(msg);
        if (string.IsNullOrEmpty(fileId))
        {
            return;
        }

        var file = await TelegramBotClient.GetFile(fileId);
        if (string.IsNullOrEmpty(file.FilePath))
        {
            return;
        }

        using var downloadFileStream = new MemoryStream();
        await TelegramBotClient.DownloadFile(file.FilePath, downloadFileStream);

        if (msg.ReplyToMessage!.Type == MessageType.Photo)
        {
            var result = await _imageManipulationService.DistortImageAsync(new DistortDto
            {
                Base64 = Convert.ToBase64String(downloadFileStream.ToArray())
            });

            using var sendFileStream = new MemoryStream(result.Base64);
            await TelegramBotClient.SendPhoto(msg.Chat.Id, new InputFileStream(sendFileStream), replyParameters: new ReplyParameters{MessageId = msg.MessageId});
        }
        else if (msg.ReplyToMessage!.Type == MessageType.Video || msg.ReplyToMessage!.Type == MessageType.Animation)
        {
            var result = await _imageManipulationService.DistortVideoAsync(new DistortDto
            {
                Base64 = Convert.ToBase64String(downloadFileStream.ToArray())
            });
            using var sendFileStream = new MemoryStream(result.Base64);
            await TelegramBotClient.SendVideo(msg.Chat.Id, new InputFileStream(sendFileStream), replyParameters: new ReplyParameters{MessageId = msg.MessageId});
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

    protected override bool ValidateMessage(Message msg, UpdateType type) =>
        type == UpdateType.Message && 
        !string.IsNullOrEmpty(msg.Text) &&
        (msg.ReplyToMessage?.Photo != null || msg.ReplyToMessage?.Video != null || msg.ReplyToMessage?.Animation != null) &&
        msg.Text?.ToLower() == "жмыхни";
}