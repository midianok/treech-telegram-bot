using System.Reflection;
using Saturn.Telegram.Lib.Operation;

namespace Saturn.Telegram.Lib.Extensions;

internal static class OperationExtensions
{
    internal static OperationBase SetService(this OperationBase operation, string fieldName, object serviceInstance)
    {
        ArgumentNullException.ThrowIfNull(operation, nameof(operation));
        ArgumentNullException.ThrowIfNull(fieldName, nameof(fieldName));

        var type = operation.GetType();
        var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

        if (field == null)
        {
            throw new ArgumentException($"Приватное поле '{fieldName}' не найдено в типе '{type.FullName}'",
                nameof(fieldName));
        }

        field.SetValue(operation, serviceInstance);
        return operation;
    }
}