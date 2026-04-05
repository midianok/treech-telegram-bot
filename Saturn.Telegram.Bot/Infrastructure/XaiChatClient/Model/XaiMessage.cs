using System.Text.Json.Serialization;

namespace Saturn.Bot.Service.Infrastructure.XaiChatClient.Model;

public class XaiMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required object Content { get; init; }

    public static XaiMessage System(string text) =>
        new() { Role = "system", Content = text };

    public static XaiMessage User(string text) =>
        new() { Role = "user", Content = text };

    public static XaiMessage UserWithImage(byte[] imageBytes, string prompt)
    {
        var base64 = Convert.ToBase64String(imageBytes);
        return new XaiMessage
        {
            Role = "user",
            Content = new List<object>
            {
                new XaiImageContent { Type = "input_image", ImageUrl = $"data:image/jpeg;base64,{base64}" },
                new XaiTextContent { Type = "input_text", Text = prompt }
            }
        };
    }
}
