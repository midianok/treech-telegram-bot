using Telegram.Bot.Types;

namespace Saturn.Bot.Service.Services.Tools;

/// <summary>State of one AI chat request shared by the tools that act in the chat.</summary>
public class ChatToolContext
{
    public ChatToolContext(Message request)
    {
        Request = request;
    }

    /// <summary>The message the bot is answering.</summary>
    public Message Request { get; }

    /// <summary>
    /// The bot already did something visible (reaction, sticker, picture, poll, reminder),
    /// so an empty text answer is fine.
    /// </summary>
    public bool ActionTaken { get; private set; }

    public void MarkActionTaken() => ActionTaken = true;
}
