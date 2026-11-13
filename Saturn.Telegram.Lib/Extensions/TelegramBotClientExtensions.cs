using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Lib.Extensions;

public static class TelegramBotClientExtensions
{
    // Telegram shows a chat action for at most 5 seconds, so it has to be repeated while the work runs
    private static readonly TimeSpan ChatActionInterval = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Runs <paramref name="work"/> and keeps the chat action (e.g. "typing…") visible until it finishes.
    /// The action stops before the method returns, so it doesn't linger after the bot sends its answer.
    /// </summary>
    public static async Task<T> WithChatActionAsync<T>(this ITelegramBotClient telegramBotClient, ChatId chatId,
        ChatAction chatAction, Func<Task<T>> work, CancellationToken cancellationToken = default)
    {
        using var stopActions = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var actionsTask = KeepChatActionAsync(telegramBotClient, chatId, chatAction, stopActions.Token);
        try
        {
            return await work();
        }
        finally
        {
            await stopActions.CancelAsync();
            await actionsTask;
        }
    }

    private static async Task KeepChatActionAsync(ITelegramBotClient telegramBotClient, ChatId chatId,
        ChatAction chatAction, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await telegramBotClient.SendChatAction(chatId, chatAction, cancellationToken: cancellationToken);
                await Task.Delay(ChatActionInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                // The indicator is cosmetic: a failed action must not break the answer, just try again later
                try
                {
                    await Task.Delay(ChatActionInterval, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    public static async Task<byte[]> DownloadFileAsync(this ITelegramBotClient telegramBotClient, string fileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileId);

        var file = await telegramBotClient.GetFile(fileId, cancellationToken: cancellationToken);
        if (string.IsNullOrEmpty(file.FilePath))
        {
            throw new FileNotFoundException("File not found", fileId);
        }

        using var fileData = new MemoryStream();
        await telegramBotClient.DownloadFile(file.FilePath, fileData, cancellationToken: cancellationToken);
        return fileData.ToArray();
    }
}