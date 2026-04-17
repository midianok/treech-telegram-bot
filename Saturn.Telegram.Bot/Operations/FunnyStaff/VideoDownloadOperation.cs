using System.Text.RegularExpressions;
using Saturn.Bot.Service.Services;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace Saturn.Bot.Service.Operations.FunnyStaff;

public partial class VideoDownloadOperation : IOperation
{
    private static readonly Regex VideoUrlRegex = BuildVideoUrlRegex();
    private static readonly Regex TikTokUrlRegex = new(@"tiktok\.com", RegexOptions.IgnoreCase);

    private readonly TelegramBotClient _telegramBotClient;

    public VideoDownloadOperation(TelegramBotClient telegramBotClient)
    {
        _telegramBotClient = telegramBotClient;
    }

    public bool Validate(Message msg, UpdateType type)
    {
        if (type != UpdateType.Message)
            return false;
        var text = msg.Text ?? msg.Caption;
        return !string.IsNullOrEmpty(text) && VideoUrlRegex.IsMatch(text);
    }

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        var text = msg.Text ?? msg.Caption ?? string.Empty;
        var match = VideoUrlRegex.Match(text);
        if (!match.Success)
            return;

        var url = match.Value;
        var tempDir = Path.Combine(AppContext.BaseDirectory, "Downloads", $"ytdlp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var ffmpegBin = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
            var ytdlBin = OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp";
            var isTikTok = TikTokUrlRegex.IsMatch(url);
            var format = isTikTok
                ? "best[height<=720]/best"
                : "bestvideo[height<=720]+bestaudio/best[height<=720]/best";

            var ytdl = new YoutubeDL
            {
                YoutubeDLPath = Path.Combine(YtDlpSetupService.ToolsFolder, ytdlBin),
                FFmpegPath = Path.Combine(YtDlpSetupService.ToolsFolder, ffmpegBin),
                OutputFolder = tempDir,
                OutputFileTemplate = "%(id)s.%(ext)s"
            };

            var result = await ytdl.RunVideoDownload(
                url: url,
                format: format,
                mergeFormat: DownloadMergeFormat.Mp4,
                recodeFormat: VideoRecodeFormat.Mp4
            );

            if (!result.Success || string.IsNullOrEmpty(result.Data))
                return;

            var filePath = result.Data;
            var fileInfo = new FileInfo(filePath);

            if (!fileInfo.Exists)
                return;

            await using var stream = fileInfo.OpenRead();
            await _telegramBotClient.SendVideo(
                chatId: msg.Chat.Id,
                video: new InputFileStream(stream, fileInfo.Name),
                replyParameters: new ReplyParameters { MessageId = msg.MessageId }
            );
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [GeneratedRegex(
        @"https?://(?:(?:www\.|vt\.|vm\.)?tiktok\.com/\S+|(?:www\.)?instagram\.com/reel/\S+|(?:www\.)?youtube\.com/shorts/\S+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex BuildVideoUrlRegex();
}
