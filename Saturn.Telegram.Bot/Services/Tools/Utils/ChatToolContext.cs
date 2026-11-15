using Telegram.Bot.Types;

namespace Saturn.Bot.Service.Services.Tools.Utils;

/// <summary>State of one AI chat request shared by the tools that act in the chat.</summary>
public class ChatToolContext
{
    public ChatToolContext(Message request, string requestText, IReadOnlyList<byte[]> images)
    {
        Request = request;
        RequestText = requestText;
        Images = images;
    }

    /// <summary>The message the bot is answering.</summary>
    public Message Request { get; }

    /// <summary>The request text without the invoke command, as the user wrote it.</summary>
    public string RequestText { get; }

    /// <summary>Pictures the request is about: the replied photo and the photo attached to the request.</summary>
    public IReadOnlyList<byte[]> Images { get; }

    /// <summary>
    /// The bot already did something visible (reaction, sticker, picture, poll, reminder),
    /// so an empty text answer is fine.
    /// </summary>
    public bool ActionTaken { get; private set; }

    public void MarkActionTaken() => ActionTaken = true;
}