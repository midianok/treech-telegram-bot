using Telegram.Bot;

namespace Saturn.Telegram.Lib.Extensions;

public static class TelegramBotClientExtensions
{
    public static async Task<byte[]> DownloadFileAsync(this TelegramBotClient telegramBotClient, string fileId) 
    {
        ArgumentException.ThrowIfNullOrEmpty(fileId);

        var file = await telegramBotClient.GetFile(fileId);
        if (string.IsNullOrEmpty(file.FilePath))
        {
            throw new FileNotFoundException("File not found", fileId);
        }

        using var fileData = new MemoryStream();
        await telegramBotClient.DownloadFile(file.FilePath, fileData);
        return fileData.ToArray();
    }
}