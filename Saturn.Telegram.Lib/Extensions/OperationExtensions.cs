using Saturn.Telegram.Lib.Operation;

namespace Saturn.Telegram.Lib.Extensions;

public static class OperationExtensions
{
    extension(IOperation operation)
    {
        public T? GetAttribute<T>() where T : Attribute =>
            operation.GetType()
                .GetCustomAttributes(typeof(T), false)
                .OfType<T>()
                .FirstOrDefault();
    }
}