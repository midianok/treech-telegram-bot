namespace Saturn.Telegram.Lib.Attributes;

/// <summary>
/// Applies a global (cross-user) rate limit to an <see cref="Saturn.Telegram.Lib.Operation.IOperation"/>.
/// The operation can be invoked at most <see cref="MaxCallsPerHour"/> times per hour regardless of who calls it.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GlobalCooldownAttribute : Attribute
{
    /// <summary>Maximum number of times the operation may be invoked across all users within one hour.</summary>
    public int MaxCallsPerHour { get; }

    /// <summary>Optional reply text sent when the global limit is reached. Falls back to a default message when <c>null</c>.</summary>
    public string? Message { get; }

    /// <param name="maxCallsPerHour">Allowed invocations per hour, shared across all users.</param>
    /// <param name="message">Custom rate-limit reply text; <c>null</c> uses the default.</param>
    public GlobalCooldownAttribute(int maxCallsPerHour, string? message = null)
    {
        MaxCallsPerHour = maxCallsPerHour;
        Message = message;
    }
}
