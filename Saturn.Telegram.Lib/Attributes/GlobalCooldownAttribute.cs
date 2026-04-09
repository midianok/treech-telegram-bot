namespace Saturn.Telegram.Lib.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GlobalCooldownAttribute : Attribute
{
    public int MaxCallsPerHour { get; }
    public string? Message { get; }

    public GlobalCooldownAttribute(int maxCallsPerHour, string? message = null)
    {
        MaxCallsPerHour = maxCallsPerHour;
        Message = message;
    }
}
