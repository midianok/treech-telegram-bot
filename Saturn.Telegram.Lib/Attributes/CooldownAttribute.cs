namespace Saturn.Telegram.Lib.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CooldownAttribute : Attribute
{
    public int Seconds { get; }
    public string? Message { get; }

    public CooldownAttribute(int seconds, string? message = null)
    {
        Seconds = seconds;
        Message = message;
    }
}
