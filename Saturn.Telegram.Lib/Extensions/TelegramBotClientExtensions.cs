using Telegram.Bot;

namespace Saturn.Telegram.Lib.Extensions;

public static class TelegramBotClientExtensions
{
    public static async Task<byte[]> DownloadFileAsync(this ITelegramBotClient telegramBotClient, string fileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileId);

        var file = await telegramBotClient.GetFile(fileId, cancellationToken: cancellationToken);
        if (string.IsNullOrEmpty(file.FilePath))
        {
            throw new FileNotFoundException("File not found", fileId);
        }

        using var fileData = new MemoryStream();
        await telegramBotClient.DownloadFile(file.FilePath, fileData, cancellationToken: cancellationToken);
        return fileData.ToArray();
    }
}