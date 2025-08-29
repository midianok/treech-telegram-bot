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
            Logger.LogError(exception, "*Error*: {Message}\n```csharp\n{StackTrace}\n```\n```json\n{json}```", exception.Message, exception.StackTrace, JsonSerializer.Serialize(msg, _jsonSerializerOptions));
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