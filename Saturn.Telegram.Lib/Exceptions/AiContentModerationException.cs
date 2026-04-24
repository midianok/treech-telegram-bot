namespace Saturn.Telegram.Lib.Exceptions;

public class AiContentModerationException : Exception
{
    public AiContentModerationException() : base("AI content moderation rejection (400)") { }
}
