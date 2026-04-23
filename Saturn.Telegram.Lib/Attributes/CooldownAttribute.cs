namespace Saturn.Telegram.Lib.Attributes;

/// <summary>
/// Applies a per-user cooldown to an <see cref="Saturn.Telegram.Lib.Operation.IOperation"/>.
/// A user who triggers the operation must wait <see cref="Seconds"/> seconds before triggering it again.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CooldownAttribute : Attribute
{
    /// <summary>Cooldown duration in seconds.</summary>
    public int Seconds { get; }

    /// <summary>Optional reply text sent to the user while on cooldown. Falls back to a default message when <c>null</c>.</summary>
    public string? Message { get; }

    /// <param name="seconds">Number of seconds a user must wait between invocations.</param>
    /// <param name="message">Custom cooldown reply text; <c>null</c> uses the default.</param>
    public CooldownAttribute(int seconds, string? message = null)
    {
        Seconds = seconds;
        Message = message;
    }
}
