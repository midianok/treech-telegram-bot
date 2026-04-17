using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Saturn.Bot.Service.Services;

public class YtDlpSetupService : IHostedService
{
    public static readonly string ToolsFolder = Path.Combine(AppContext.BaseDirectory, "Tools");
    public static readonly string YtDlpBinaryName = OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp";

    private static string GetDownloadUrl()
    {
        if (OperatingSystem.IsWindows())
            return "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        if (OperatingSystem.IsMacOS())
            return "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_macos";
        return "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_linux";
    }

    private readonly ILogger<YtDlpSetupService> _logger;

    public YtDlpSetupService(ILogger<YtDlpSetupService> logger)
    {
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(ToolsFolder);
        var ytDlpBinary = Path.Combine(ToolsFolder, YtDlpBinaryName);

        if (File.Exists(ytDlpBinary))
        {
            _logger.LogInformation("yt-dlp found at {Path}", ytDlpBinary);
            return;
        }

        _logger.LogInformation("yt-dlp not found, downloading...");
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

        var bytes = await httpClient.GetByteArrayAsync(GetDownloadUrl(), cancellationToken);
        await File.WriteAllBytesAsync(ytDlpBinary, bytes, cancellationToken);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(ytDlpBinary,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        _logger.LogInformation("yt-dlp downloaded to {Path}", ytDlpBinary);
    }

    public static async Task RunSelfUpdateAsync(ILogger logger, CancellationToken cancellationToken)
    {
        var ytDlpBinary = Path.Combine(ToolsFolder, YtDlpBinaryName);
        logger.LogInformation("Updating yt-dlp...");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ytDlpBinary,
                Arguments = "-U",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var log = string.IsNullOrWhiteSpace(output) ? error : output;
        logger.LogInformation("yt-dlp update result: {Output}", log.Trim());
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
