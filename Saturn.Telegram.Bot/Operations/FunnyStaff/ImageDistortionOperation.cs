using Saturn.Bot.Service.Extensions;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.FunnyStaff;

public class ImageDistortionOperation : IOperation
{
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IDistortionService _distortionService;

    public ImageDistortionOperation(ITelegramBotClient telegramBotClient, IDistortionService distortionService)
    {
        _telegramBotClient = telegramBotClient;
        _distortionService = distortionService;
    }

    public bool Validate(Message msg, UpdateType type) =>
        type == UpdateType.Message &&
        (msg.ReplyToMessage?.Photo != null ||
         msg.ReplyToMessage?.Video != null ||
         msg.ReplyToMessage?.Animation != null ||
         msg.ReplyToMessage?.Sticker != null ||
         msg.ReplyToMessage?.VideoNote != null) &&
        msg.HasText("жмыхни");

    public async Task OnMessageAsync(Message msg, UpdateType type, CancellationToken сancellationToken)
    {
        var fileId = GetFileId(msg);
        if (string.IsNullOrEmpty(fileId))
            return;

        var file = await _telegramBotClient.GetFile(fileId, cancellationToken: сancellationToken);
        if (string.IsNullOrEmpty(file.FilePath))
            return;

        using var downloadStream = new MemoryStream();
        await _telegramBotClient.DownloadFile(file.FilePath, downloadStream, cancellationToken: сancellationToken);
        var fileBytes = downloadStream.ToArray();

        var replyParams = new ReplyParameters { MessageId = msg.MessageId };
        var replyType = msg.ReplyToMessage!.Type;

        if (replyType == MessageType.Sticker && msg.ReplyToMessage.Sticker!.IsAnimated)
        {
            await _telegramBotClient.SendMessage(msg.Chat.Id, "Анимированные стикеры (TGS) не поддерживаются",
                replyParameters: replyParams, cancellationToken: сancellationToken);
            return;
        }

        var isImageLike = replyType == MessageType.Photo ||
                          (replyType == MessageType.Sticker && !msg.ReplyToMessage.Sticker!.IsVideo);

        if (isImageLike)
        {
            var resultBytes = _distortionService.DistortImage(fileBytes);
            using var sendStream = new MemoryStream(resultBytes);
            await _telegramBotClient.SendPhoto(msg.Chat.Id, new InputFileStream(sendStream),
                replyParameters: replyParams, cancellationToken: сancellationToken);
        }
        else
        {
            var progressMsg = await _telegramBotClient.SendMessage(msg.Chat.Id, "Жмыхаем",
                replyParameters: replyParams, cancellationToken: сancellationToken);

            var onProgress = CreateProgressCallback(msg.Chat.Id, progressMsg.MessageId, сancellationToken);

            byte[] resultBytes;
            try
            {
                resultBytes = await _distortionService.DistortVideoAsync(fileBytes, onProgress, сancellationToken);
            }
            finally
            {
                await _telegramBotClient.DeleteMessage(msg.Chat.Id, progressMsg.MessageId, cancellationToken: сancellationToken);
            }

            using var sendStream = new MemoryStream(resultBytes);
            if (replyType == MessageType.VideoNote)
            {
                await _telegramBotClient.SendVideoNote(msg.Chat.Id, new InputFileStream(sendStream),
                    replyParameters: replyParams, cancellationToken: сancellationToken);
            }
            else
            {
                await _telegramBotClient.SendVideo(msg.Chat.Id, new InputFileStream(sendStream),
                    replyParameters: replyParams, cancellationToken: сancellationToken);
            }
        }
    }

    private Func<int, Task> CreateProgressCallback(long chatId, int messageId, CancellationToken cancellationToken)
    {
        var lastReported = -1;
        var lastUpdateTime = DateTime.MinValue;
        return async percent =>
        {
            if (percent == lastReported)
            {
                return;
            }
            
            var now = DateTime.UtcNow;
            if (percent < 100 && (now - lastUpdateTime).TotalMilliseconds < 1000)
            {
                return;
            }
            
            lastReported = percent;
            lastUpdateTime = now;

            try
            {
                await _telegramBotClient.EditMessageText(chatId, messageId, $"Жмыхнуто на {percent}%",
                    cancellationToken: cancellationToken);
            }
            catch
            {
                 /* ignore rate limit errors */
            }
        };
    }

    private string GetFileId(Message msg) =>
        msg.ReplyToMessage!.Type switch
        {
            MessageType.Photo => msg.ReplyToMessage!.Photo!.MaxBy(x => x.FileSize)!.FileId,
            MessageType.Video => msg.ReplyToMessage!.Video!.FileId,
            MessageType.Animation => msg.ReplyToMessage!.Animation!.FileId,
            MessageType.Sticker => msg.ReplyToMessage!.Sticker!.FileId,
            MessageType.VideoNote => msg.ReplyToMessage!.VideoNote!.FileId,
            _ => string.Empty
        };
}
