using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.FunnyStaff;

public class ImageDistortionOperation : IOperation
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly IDistortionService _distortionService;

    public ImageDistortionOperation(TelegramBotClient telegramBotClient, IDistortionService distortionService)
    {
        _telegramBotClient = telegramBotClient;
        _distortionService = distortionService;
    }

    public bool Validate(Message msg, UpdateType type) =>
        type == UpdateType.Message &&
        !string.IsNullOrEmpty(msg.Text) &&
        (msg.ReplyToMessage?.Photo != null || msg.ReplyToMessage?.Video != null || msg.ReplyToMessage?.Animation != null) &&
        msg.Text.ToLower() == "жмыхни";

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        var fileId = GetFileId(msg);
        if (string.IsNullOrEmpty(fileId))
            return;

        var file = await _telegramBotClient.GetFile(fileId);
        if (string.IsNullOrEmpty(file.FilePath))
            return;

        using var downloadStream = new MemoryStream();
        await _telegramBotClient.DownloadFile(file.FilePath, downloadStream);
        var fileBytes = downloadStream.ToArray();

        if (msg.ReplyToMessage!.Type == MessageType.Photo)
        {
            var resultBytes = _distortionService.DistortImage(fileBytes);
            using var sendStream = new MemoryStream(resultBytes);
            await _telegramBotClient.SendPhoto(msg.Chat.Id, new InputFileStream(sendStream),
                replyParameters: new ReplyParameters { MessageId = msg.MessageId });
        }
        else if (msg.ReplyToMessage!.Type == MessageType.Video || msg.ReplyToMessage!.Type == MessageType.Animation)
        {
            var resultBytes = await _distortionService.DistortVideoAsync(fileBytes);
            using var sendStream = new MemoryStream(resultBytes);
            await _telegramBotClient.SendVideo(msg.Chat.Id, new InputFileStream(sendStream),
                replyParameters: new ReplyParameters { MessageId = msg.MessageId });
        }
    }

    public Task OnUpdateAsync(Update update) => Task.CompletedTask;

    private string GetFileId(Message msg) =>
        msg.ReplyToMessage!.Type switch
        {
            MessageType.Photo => msg.ReplyToMessage!.Photo!.MaxBy(x => x.FileSize)!.FileId,
            MessageType.Video => msg.ReplyToMessage!.Video!.FileId,
            MessageType.Animation => msg.ReplyToMessage!.Animation!.FileId,
            _ => string.Empty
        };
}
