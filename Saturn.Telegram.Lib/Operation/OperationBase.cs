using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Lib.Operation;

public abstract class OperationBase
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
        catch (Exception exception)
        {
            await TelegramBotClient.SendMessage(msg.Chat, "что-то пошло не так", ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id });
            var json = JsonSerializer.Serialize(msg, _jsonSerializerOptions);
            await TelegramBotClient.SendMessage(-4899665219, $"Error: *{exception.Message}*\nStackTrace:\n```csharp\n{exception.StackTrace}\n```\n```json\n{json}```", ParseMode.Markdown);
        }
    }
    
    public Task OnUpdateAsync(Update update) => 
        ProcessOnUpdateAsync(update);
    
    public Task OnErrorAsync(Exception exception, HandleErrorSource source) => 
        Task.CompletedTask;

    protected virtual Task ProcessOnMessageAsync(Message msg, UpdateType type) => Task.CompletedTask;
    
    protected virtual Task ProcessOnUpdateAsync(Update update) => Task.CompletedTask;
    
    protected virtual bool ValidateMessage(Message msg, UpdateType type) => true;
}