using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Lib;

public class OperationManager
{
    private readonly IEnumerable<OperationBase> _operations;
    private readonly ILogger<OperationManager> _logger;
    
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping 
    };

    public OperationManager(IEnumerable<OperationBase> operations, ILogger<OperationManager> logger)
    {
        _operations = operations;
        _logger = logger;
    }

    public async Task MessageHandler(Message msg, UpdateType type)
    {
        foreach (var operation in _operations)
        {
            try
            {
                await operation.OnMessageAsync(msg, type);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "*Error*: {Message}\n```csharp\n{StackTrace}\n```\n```json\n{json}```", exception.Message, exception.StackTrace, JsonSerializer.Serialize(msg, _jsonSerializerOptions));
            }
        }
    }
    
    public Task UpdateHandler(Update update) => 
        Task.CompletedTask;

    public Task ErrorHandler(Exception exception, HandleErrorSource source) => 
        Task.CompletedTask;
}