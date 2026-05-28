using OpenAI.Chat;

namespace Saturn.Bot.Service.Services.Abstractions;

public interface IAiService
{
    Task<string> CompleteChatAsync(IList<ChatMessage> messages, CancellationToken ct = default);
    Task<string> CompleteChatAsync(IList<ChatMessage> messages, IReadOnlyList<IChatTool> tools, CancellationToken ct = default);
    Task<byte[]> GenerateImageAsync(string prompt);
    Task<byte[]> EditImageAsync(IReadOnlyList<byte[]> images, string prompt);
    Task<byte[]> GenerateVideoFromImageAsync(byte[] image, string? prompt, string aspectRatio, CancellationToken ct = default);
}
