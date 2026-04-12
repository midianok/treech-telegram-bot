using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace Saturn.Bot.Service.Services;

public class FfmpegSetupService : IHostedService
{
    private const string FfmpegFolder = "Tools";
    private readonly ILogger<FfmpegSetupService> _logger;

    public FfmpegSetupService(ILogger<FfmpegSetupService> logger)
    {
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(FfmpegFolder);
        FFmpeg.SetExecutablesPath(FfmpegFolder);

        var ffmpegBinary = Path.Combine(FfmpegFolder, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        if (!File.Exists(ffmpegBinary))
        {
            _logger.LogInformation("ffmpeg not found, downloading...");
            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, FfmpegFolder);
            _logger.LogInformation("ffmpeg downloaded to {Folder}", FfmpegFolder);
        }
        else
        {
            _logger.LogInformation("ffmpeg found at {Path}", ffmpegBinary);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
