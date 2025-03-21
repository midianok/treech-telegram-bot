using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Lib.Attributes;

public class AccessAttribute : Attribute
{
    public AccessAttribute(ChatMemberStatus status, string message)
    {
        Status = status;
        Message = message;
    }
    
    public AccessAttribute(string userName, string message)
    {
        UserName = userName;
        Message = message;
    }
    public ChatMemberStatus Status { get; }

    public string? UserName { get; }

    public string Message { get; set; }
}