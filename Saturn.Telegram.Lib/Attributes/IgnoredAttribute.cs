namespace Saturn.Telegram.Lib.Attributes;

/// <summary>
/// Marks an <see cref="Saturn.Telegram.Lib.Operation.IOperation"/> implementation as ignored by <c>OperationManager</c>.
/// The operation is registered in DI but never dispatched to during message processing.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IgnoredAttribute : Attribute;
