namespace Saturn.Telegram.Lib.Attributes;

/// <summary>
/// Restricts an <see cref="Saturn.Telegram.Lib.Operation.IOperation"/> to group chats only.
/// When a user invokes the operation in a private chat, <see cref="Message"/> is sent as a reply.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ChatOnlyAttribute : Attribute
{
    public const string DefaultMessage = "Эта команда доступна только в групповых чатах.";

    /// <summary>The reply message sent to the user when the operation is called outside a group chat.</summary>
    public string Message { get; }

    /// <param name="message">Custom reply text; defaults to <see cref="DefaultMessage"/>.</param>
    public ChatOnlyAttribute(string message = DefaultMessage)
    {
        Message = message;
    }
}
