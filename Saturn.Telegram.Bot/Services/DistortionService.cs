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

            var audioPath = Path.Combine(fileTempDir, $"{id}_audio.wav");
            var distortedAudioPath = Path.Combine(fileTempDir, $"{id}_audio_distorted.wav");

            _logger.LogInformation("Distorting {Count} frames (parallel) + extracting audio", framePaths.Count);

            var completed = 0;
            var framesTask = Parallel.ForEachAsync(
                framePaths.Select((path, i) => (path, i)),
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                async (item, ct) =>
                {
                    using var image = new MagickImage(item.path);
                    image.LiquidRescale(new Percentage(40), new Percentage(40), 1, 0);
                    image.Resize(image.Width, image.Height);
                    await image.WriteAsync(Path.Combine(distortedDir, $"frame_{item.i + 1}.png"), ct);

                    if (onProgress != null)
                    {
                        var done = Interlocked.Increment(ref completed);
                        await onProgress(done * 100 / framePaths.Count);
                    }
                });

            var audioTask = Task.Run(async () =>
            {
                using var extractProcess = new Process();
                extractProcess.StartInfo.CreateNoWindow = true;
                extractProcess.StartInfo.FileName = ffmpegExe;
                extractProcess.StartInfo.Arguments = $"-i \"{videoFilePath}\" -vn -y \"{audioPath}\"";
                extractProcess.Start();
                await extractProcess.WaitForExitAsync();
                return extractProcess.ExitCode == 0 && new FileInfo(audioPath).Length > 0;
            });

            await Task.WhenAll(framesTask, audioTask);

            var hasAudio = await audioTask;
            if (hasAudio)
            {
                using var process = new Process();
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.FileName = ffmpegExe;
                process.StartInfo.Arguments = $"-i \"{audioPath}\" -af \"vibrato=f=10:d=0.8,tremolo=f=8:d=0.9\" -y \"{distortedAudioPath}\"";
                process.Start();
                await process.WaitForExitAsync();
                hasAudio = process.ExitCode == 0 && new FileInfo(distortedAudioPath).Length > 0;
            }
            _logger.LogInformation("Audio extracted and distorted: {HasAudio}", hasAudio);

            var outputVideoPath = Path.Combine(fileTempDir, $"{id}_output.mp4");
            using (var process = new Process())
            {
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.FileName = ffmpegExe;
                var audioArgs = hasAudio ? $"-i \"{distortedAudioPath}\" " : "";
                var audioCodec = hasAudio ? "-c:a aac -shortest " : "";
                process.StartInfo.Arguments = $"-y -framerate 15 -i \"{Path.Combine(distortedDir, "frame_%d.png")}\" {audioArgs}-vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -pix_fmt yuv420p {audioCodec}\"{outputVideoPath}\"";
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
