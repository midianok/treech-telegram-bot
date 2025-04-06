using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations;

public class UploadOperation : OperationBase
{
    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var fileId = msg.ReplyToMessage!.Photo!.MaxBy(x => x.FileSize)!.FileId;
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
        var fileName = Guid.NewGuid().ToString()[..8];
        var filePath = $"/app/files/{fileName}.jpg";
        
        await File.WriteAllBytesAsync(filePath, downloadFileStream.ToArray());
        
        await TelegramBotClient.SendMessage(msg.Chat, $"https://treech.ru/st/{fileName}.jpg");
    }  

    protected override bool ValidateOnMessage(Message msg, UpdateType type) =>
        type == UpdateType.Message &&
        !string.IsNullOrEmpty(msg.Text) &&
        msg.ReplyToMessage?.Photo != null &&
        msg.Text.ToLower() == "загрузи";
}