using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Services.Abstractions;

public interface IGenerationProgressService
{
    /// <summary>
    /// Отвечает на <paramref name="replyTo"/> статусным сообщением и обновляет его, пока идёт генерация.
    /// После доставки результата статус удаляется, при ошибке — заменяется текстом ошибки. Исключения пробрасываются дальше.
    /// </summary>
    Task RunAsync<T>(
        Message replyTo,
        GenerationProgressOptions options,
        Func<Action<string>, Task<T>> generate,
        Func<T, Task> deliver,
        CancellationToken cancellationToken);
}

public record GenerationProgressOptions(
    string Title,
    string ProcessingStage,
    ChatAction ChatAction,
    TimeSpan ExpectedDuration,
    string FailureText);
