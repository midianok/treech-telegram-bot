using OpenAI.Chat;

namespace Saturn.Bot.Service.Services.Abstractions;

public interface IAiService
{
    Task<string> CompleteChatAsync(IList<ChatMessage> messages, CancellationToken сancellationToken);
    Task<string> CompleteChatAsync(IList<ChatMessage> messages, IReadOnlyList<IChatTool> tools, CancellationToken сancellationToken);
    Task<byte[]> GenerateImageAsync(string prompt);
    Task<byte[]> EditImageAsync(IReadOnlyList<byte[]> images, string prompt);
    Task<byte[]> GenerateVideoFromImageAsync(byte[] image, string? prompt, string aspectRatio, bool generateAudio, CancellationToken сancellationToken);
}
