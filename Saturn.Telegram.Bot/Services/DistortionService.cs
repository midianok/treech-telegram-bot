using System.Diagnostics;
using ImageMagick;
using Microsoft.Extensions.Logging;
using Saturn.Bot.Service.Services.Abstractions;

namespace Saturn.Bot.Service.Services;

public class DistortionService : IDistortionService
{
    private readonly ILogger<DistortionService> _logger;
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

    public async Task<byte[]> DistortVideoAsync(byte[] video)
    {
        var totalStopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Start distorting video");
        var id = Guid.NewGuid();
        var fileTempDir = Path.Combine(TempDirectory, id.ToString());
        try
        {
            Directory.CreateDirectory(fileTempDir);
            _logger.LogInformation("Frames directory created: {FileTempDir}", fileTempDir);

            var videoFilePath = Path.Combine(fileTempDir, $"{id}.mp4");
            await File.WriteAllBytesAsync(videoFilePath, video);
            _logger.LogInformation("File saved: {Path} ({Size} KB)", videoFilePath, video.Length / 1000);

            using (var process = new Process())
            {
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = false;
                process.StartInfo.FileName = "ffmpeg";
                process.StartInfo.Arguments = $"-i {videoFilePath} -r 15 {Path.Combine(fileTempDir, id.ToString())}_%d.png";
                process.Start();
                await process.WaitForExitAsync();
            }
            _logger.LogInformation("Frames extracted");

            var frames = Directory.GetFiles(fileTempDir, $"{id}*.png")
                .OrderBy(x => x.Length)
                .ThenBy(x => x)
                .Select(x => new MagickImage(x))
                .ToList();

            using var result = new MagickImageCollection();
            _logger.LogInformation("Distorting {Count} frames", frames.Count);
            var frameNum = 1;
            foreach (var image in frames)
            {
                var w = image.Width;
                var h = image.Height;
                image.LiquidRescale(new Percentage(40), new Percentage(40), 1, 0);
                image.Resize(w, h);
                result.Add(image);
                _logger.LogInformation("Frame {N} distorted", frameNum++);
            }

            using var memoryStream = new MemoryStream();
            await result.WriteAsync(memoryStream, MagickFormat.Mp4);
            totalStopwatch.Stop();
            _logger.LogInformation("Video distortion finished. Elapsed time: {Elapsed} sec", totalStopwatch.ElapsedMilliseconds / 1000.0);
            return memoryStream.ToArray();
        }
        finally
        {
            Directory.Delete(fileTempDir, true);
        }
    }
}
