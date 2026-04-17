using Microsoft.Extensions.Logging;
using Saturn.Bot.Service.Services;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace Saturn.Bot.Service.Operations.FunnyStaff;

public class MusicDownloadOperation(TelegramBotClient telegramBotClient, ILogger<MusicDownloadOperation> logger) : IOperation
{
    private const string Prefix = "найти ";

    public bool Validate(Message msg, UpdateType type)
    {
        if (type != UpdateType.Message)
            return false;
        var text = msg.Text ?? msg.Caption;
        return !string.IsNullOrEmpty(text) &&
               text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
    }

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        var text = msg.Text ?? msg.Caption ?? string.Empty;
        var query = text[Prefix.Length..].Trim();
        if (string.IsNullOrEmpty(query))
            return;

        var tempDir = Path.Combine(AppContext.BaseDirectory, "Downloads", $"music_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var ytdlBin = OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp";
            var ffmpegBin = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";

            var ytdl = new YoutubeDL
            {
                YoutubeDLPath = Path.Combine(YtDlpSetupService.ToolsFolder, ytdlBin),
                FFmpegPath = Path.Combine(YtDlpSetupService.ToolsFolder, ffmpegBin),
                OutputFolder = tempDir,
                OutputFileTemplate = "%(title)s.%(ext)s"
            };

            var overrideOptions = new OptionSet();
            overrideOptions.AddCustomOption("--js-runtimes", "node");

            var result = await ytdl.RunAudioDownload(
                url: $"ytsearch1:{query}",
                format: AudioConversionFormat.Mp3,
                overrideOptions: overrideOptions,
                output: new Progress<string>(line => logger.LogInformation("{Line}", line))
            );

            if (!result.Success || string.IsNullOrEmpty(result.Data))
            {
                if (!result.Success || string.IsNullOrEmpty(result.Data))
                {
                    logger.LogError("Error: {Error}", string.Join(',', result.ErrorOutput));
                    return;
                }
                await telegramBotClient.SendMessage(
                    chatId: msg.Chat.Id,
                    text: "Не удалось найти или скачать трек.",
                    replyParameters: new ReplyParameters { MessageId = msg.MessageId }
                );
                return;
            }

            var fileInfo = new FileInfo(result.Data);
            if (!fileInfo.Exists)
                return;

            await using var stream = fileInfo.OpenRead();
            await telegramBotClient.SendAudio(
                chatId: msg.Chat.Id,
                audio: new InputFileStream(stream, fileInfo.Name),
                title: Path.GetFileNameWithoutExtension(fileInfo.Name),
                replyParameters: new ReplyParameters { MessageId = msg.MessageId }
            );
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
