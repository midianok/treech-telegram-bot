using System.Diagnostics;
using ImageMagick;
using Microsoft.Extensions.Logging;
using Saturn.Bot.Service.Services.Abstractions;
using Xabe.FFmpeg;

namespace Saturn.Bot.Service.Services;

public class DistortionService : IDistortionService
{
    private readonly ILogger<DistortionService> _logger;
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);
    private const string TempDirectory = "Temp";

    public DistortionService(ILogger<DistortionService> logger)
    {
        _logger = logger;
    }

    public byte[] DistortImage(byte[] imageBytes)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Start distorting image");
        using var image = new MagickImage(imageBytes);
        var imageWidth = image.Width;
        var imageHeight = image.Height;
        image.LiquidRescale(new Percentage(40), new Percentage(40), 1, 0);
        image.Resize(imageWidth, imageHeight);
        var result = image.ToByteArray();
        stopwatch.Stop();
        _logger.LogInformation("Image distorted. Elapsed time: {Elapsed} sec", stopwatch.ElapsedMilliseconds / 1000.0);
        return result;
    }

    public async Task<byte[]> DistortVideoAsync(byte[] video, Func<int, Task>? onProgress = null)
    {
        await _semaphoreSlim.WaitAsync();
        var id = Guid.NewGuid();
        var fileTempDir = Path.Combine(TempDirectory, id.ToString());
        try
        {
            var totalStopwatch = Stopwatch.StartNew();
            _logger.LogInformation("Start distorting video");
                
            Directory.CreateDirectory(fileTempDir);
            _logger.LogInformation("Frames directory created: {FileTempDir}", fileTempDir);

            var videoFilePath = Path.Combine(fileTempDir, $"{id}.mp4");
            await File.WriteAllBytesAsync(videoFilePath, video);
            _logger.LogInformation("File saved: {Path} ({Size} KB)", videoFilePath, video.Length / 1000);

            var ffmpegExe = FFmpeg.ExecutablesPath != null
                ? Path.Combine(FFmpeg.ExecutablesPath, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg")
                : "ffmpeg";

            using (var process = new Process())
            {
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.FileName = ffmpegExe;
                process.StartInfo.Arguments = $"-i \"{videoFilePath}\" -r 15 \"{Path.Combine(fileTempDir, id.ToString())}_%d.png\"";
                process.Start();
                await process.WaitForExitAsync();
            }
            _logger.LogInformation("Frames extracted");

            var framePaths = Directory.GetFiles(fileTempDir, $"{id}*.png")
                .OrderBy(x => x.Length)
                .ThenBy(x => x)
                .ToList();

            var distortedDir = Path.Combine(fileTempDir, "distorted");
            Directory.CreateDirectory(distortedDir);

            _logger.LogInformation("Distorting {Count} frames", framePaths.Count);
            for (var i = 0; i < framePaths.Count; i++)
            {
                using var image = new MagickImage(framePaths[i]);
                image.LiquidRescale(new Percentage(40), new Percentage(40), 1, 0);
                image.Resize(image.Width, image.Height);
                
                await image.WriteAsync(Path.Combine(distortedDir, $"frame_{i + 1}.png"));
                _logger.LogInformation("Frame {N} distorted", i + 1);

                if (onProgress != null)
                {
                    await onProgress((i + 1) * 100 / framePaths.Count);
                }
            }

            var outputVideoPath = Path.Combine(fileTempDir, $"{id}_output.mp4");
            using (var process = new Process())
            {
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.FileName = ffmpegExe;
                process.StartInfo.Arguments = $"-y -framerate 15 -i \"{Path.Combine(distortedDir, "frame_%d.png")}\" -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -pix_fmt yuv420p \"{outputVideoPath}\"";
                process.Start();
                await process.WaitForExitAsync();
            }
            _logger.LogInformation("Frames reassembled");

            var result = await File.ReadAllBytesAsync(outputVideoPath);
            totalStopwatch.Stop();
            _logger.LogInformation("Video distortion finished. Elapsed time: {Elapsed} sec", totalStopwatch.ElapsedMilliseconds / 1000.0);
            return result;
        }
        finally
        {
            Directory.Delete(fileTempDir, true);
            _semaphoreSlim.Release();
        }
    }
}
