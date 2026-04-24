using OpenAI.Chat;
using OpenAI.Images;

namespace Saturn.Bot.Service.Services.Abstractions;

public interface IAiService
{
    Task<string> CompleteChatAsync(IList<ChatMessage> messages, CancellationToken ct = default);
    Task<GeneratedImage> GenerateImageAsync(string prompt, ImageGenerationOptions? options = null);
    Task<byte[]> EditImageAsync(IReadOnlyList<byte[]> images, string prompt);
    Task<byte[]> GenerateVideoFromImageAsync(byte[] image, CancellationToken ct = default);
}
