namespace Saturn.Telegram.Lib.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AllowAttribute(params string[] usernames) : Attribute
{
    public string[] Usernames { get; } = usernames;
}
