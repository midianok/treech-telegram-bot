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

    public async Task<byte[]> DistortVideoAsync(byte[] video, Func<int, Task> onProgress, CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var ct = linkedCts.Token;

        await _semaphoreSlim.WaitAsync(ct);
        var id = Guid.NewGuid();
        var fileTempDir = Path.Combine(TempDirectory, id.ToString());
        try
        {
            var totalStopwatch = Stopwatch.StartNew();
            _logger.LogInformation("Start distorting video");

            Directory.CreateDirectory(fileTempDir);
            _logger.LogInformation("Frames directory created: {FileTempDir}", fileTempDir);

            var videoFilePath = Path.Combine(fileTempDir, $"{id}.mp4");
            await File.WriteAllBytesAsync(videoFilePath, video, ct);
            _logger.LogInformation("File saved: {Path} ({Size} KB)", videoFilePath, video.Length / 1000);

            var ffmpegExe = FFmpeg.ExecutablesPath != null
                ? Path.Combine(FFmpeg.ExecutablesPath, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg")
                : "ffmpeg";

            await RunProcessAsync(ffmpegExe,
                $"-i \"{videoFilePath}\" -r 15 \"{Path.Combine(fileTempDir, id.ToString())}_%d.png\"",
                ct);
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
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = ct },
                async (item, token) =>
                {
                    using var image = new MagickImage(item.path);
                    image.LiquidRescale(new Percentage(40), new Percentage(40), 1, 0);
                    image.Resize(image.Width, image.Height);
                    await image.WriteAsync(Path.Combine(distortedDir, $"frame_{item.i + 1}.png"), token);

                    if (onProgress != null)
                    {
                        var done = Interlocked.Increment(ref completed);
                        await onProgress(done * 100 / framePaths.Count);
                    }
                });

            var audioTask = Task.Run(async () =>
            {
                var exitCode = await RunProcessAsync(ffmpegExe, $"-i \"{videoFilePath}\" -vn -y \"{audioPath}\"", ct);
                return exitCode == 0 && new FileInfo(audioPath).Length > 0;
            }, ct);

            await Task.WhenAll(framesTask, audioTask);

            var hasAudio = await audioTask;
            if (hasAudio)
            {
                var exitCode = await RunProcessAsync(ffmpegExe,
                    $"-i \"{audioPath}\" -af \"vibrato=f=10:d=0.8,tremolo=f=8:d=0.9\" -y \"{distortedAudioPath}\"",
                    ct);
                hasAudio = exitCode == 0 && new FileInfo(distortedAudioPath).Length > 0;
            }
            _logger.LogInformation("Audio extracted and distorted: {HasAudio}", hasAudio);

            var outputVideoPath = Path.Combine(fileTempDir, $"{id}_output.mp4");
            var audioArgs = hasAudio ? $"-i \"{distortedAudioPath}\" " : "";
            var audioCodec = hasAudio ? "-c:a aac -shortest " : "";
            await RunProcessAsync(ffmpegExe,
                $"-y -framerate 15 -i \"{Path.Combine(distortedDir, "frame_%d.png")}\" {audioArgs}-vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -pix_fmt yuv420p {audioCodec}\"{outputVideoPath}\"",
                ct);
            _logger.LogInformation("Frames reassembled");

            var result = await File.ReadAllBytesAsync(outputVideoPath, ct);
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

    private static async Task<int> RunProcessAsync(string fileName, string arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(outputTask, errorTask);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
        return process.ExitCode;
    }
}
