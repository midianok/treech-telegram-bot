namespace Saturn.Bot.Service.Services.Abstractions;

public interface IDistortionService
{
    byte[] DistortImage(byte[] imageBytes);
    Task<byte[]> DistortVideoAsync(byte[] video, Func<int, Task>? onProgress = null);
}