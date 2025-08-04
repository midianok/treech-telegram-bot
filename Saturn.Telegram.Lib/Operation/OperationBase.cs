using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Lib.Operation;

public abstract class OperationBase : IOperation
{
    protected readonly ILogger<OperationBase> Logger;
    protected readonly TelegramBotClient TelegramBotClient;

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        var isMatch = ValidateMessage(msg, type);
        if (!isMatch)
        {
            return;
        }

        using var typingCancellationTokenSource = new CancellationTokenSource();
        _ = SendTypingAsync(msg.Chat.Id, typingCancellationTokenSource.Token);
        
        try
        {
            await ProcessOnMessageAsync(msg, type);
        }
        catch (Exception e)
        {
            await TelegramBotClient.SendMessage(msg.Chat, "что-то пошло не так", ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id });
            await typingCancellationTokenSource.CancelAsync();
            Logger.LogError(e, e.Message);
        }
        
    }
    private async Task SendTypingAsync(long chatId, CancellationToken cancellationToken)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        using var combinedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellationTokenSource.Token);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await TelegramBotClient.SendChatAction(chatId, ChatAction.Typing,
                    cancellationToken: combinedCancellationTokenSource.Token);
                await Task.Delay(TimeSpan.FromSeconds(5), combinedCancellationTokenSource.Token);
            }
        }
        catch (Exception)
        {
            // ignored
        }
    }
    
    public Task OnUpdateAsync(Update update)
    {
        return ProcessOnUpdateAsync(update);
    }

    public Task OnErrorAsync(Exception exception, HandleErrorSource source)
    {
        Logger.LogError(exception, "Ошибка");
        return Task.CompletedTask;
    }
    
    protected virtual bool ValidateMessage(Message msg, UpdateType type) => true;

    protected virtual Task ProcessOnMessageAsync(Message msg, UpdateType type) => Task.CompletedTask;
    
    protected virtual Task ProcessOnUpdateAsync(Update update) => Task.CompletedTask;
}