namespace Saturn.Telegram.Lib.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AllowAttribute : Attribute
{
    public long[] UserIds { get; }

    public AllowAttribute(params long[] userIds)
    {
        UserIds = userIds;
    }
}