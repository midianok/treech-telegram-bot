namespace Saturn.Telegram.Lib.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ChatOnlyAttribute : Attribute
{
    public const string DefaultMessage = "Эта команда доступна только в групповых чатах.";

    public string Message { get; }

    public ChatOnlyAttribute(string message = DefaultMessage)
    {
        Message = message;
    }
}
