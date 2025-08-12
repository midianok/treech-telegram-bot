using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
    };

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        var isMatch = ValidateMessage(msg, type);
        if (!isMatch)
        {
            return;
        }
        
        try
        {
            await ProcessOnMessageAsync(msg, type);
        }
        catch (Exception e)
        {
            await TelegramBotClient.SendMessage(msg.Chat, "что-то пошло не так", ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id });
            var json = JsonSerializer.Serialize(msg, _jsonSerializerOptions);
            await TelegramBotClient.SendMessage(-4899665219, $"Error: *{e.Message}*\nStackTrace:\n```csharp\n{e.StackTrace}\n```\n```json\n{json}```", ParseMode.Markdown);
            Logger.LogError(e, e.Message);
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