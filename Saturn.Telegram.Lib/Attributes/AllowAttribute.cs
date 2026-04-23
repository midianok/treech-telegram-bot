namespace Saturn.Telegram.Lib.Attributes;

/// <summary>
/// Restricts an <see cref="Saturn.Telegram.Lib.Operation.IOperation"/> to a specific set of Telegram user IDs.
/// Messages from users not in <see cref="UserIds"/> are ignored by the operation.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AllowAttribute : Attribute
{
    /// <summary>The Telegram user IDs permitted to invoke the operation.</summary>
    public long[] UserIds { get; }

    /// <param name="userIds">One or more Telegram user IDs that are allowed to use the operation.</param>
    public AllowAttribute(params long[] userIds)
    {
        UserIds = userIds;
    }
}