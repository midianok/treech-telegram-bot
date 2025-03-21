using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Lib.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class CooldownAttribute : Attribute
{
    public CooldownAttribute(long chatId, ChatMemberStatus userStatus, int cooldown, string message)
    {
        UserStatus = userStatus;
        Cooldown = cooldown;
        Message = message;
        ChatId = chatId;
    }
    
    public CooldownAttribute(long chatId, string userName, int cooldown, string message)
    {
        UserName = userName;
        Cooldown = cooldown;
        ChatId = chatId;
        Message = message;
        UserStatus = ChatMemberStatus.Member;
    }
    
    public CooldownAttribute(long chatId, int cooldown, string message)
    {
        Message = message;
        ChatId = chatId;
        Cooldown = cooldown;
    }

    public ChatMemberStatus UserStatus { get; }

    public string? UserName { get; }
    
    public int Cooldown { get; }

    public string Message { get; }

    public long ChatId { get; }
}