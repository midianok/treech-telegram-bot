using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Saturn.Telegram.Lib.Attributes;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Infrastructure;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Lib;

public class OperationManager
{
    private readonly IEnumerable<IOperation> _operations;
    private readonly ILogger<OperationManager> _logger;
    private readonly ICooldownService _cooldownService;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public OperationManager(
        IEnumerable<IOperation> operations,
        ILogger<OperationManager> logger,
        ICooldownService cooldownService)
    {
        _operations = operations;
        _logger = logger;
        _cooldownService = cooldownService;
    }

    public async Task MessageHandler(Message msg, UpdateType type)
    {
        foreach (var operation in _operations)
        {
            if (!operation.Validate(msg, type))
            {
                continue;
            }

            if (!IsAllowed(msg.From?.Id, operation))
            {
                continue;
            }

            if (await _cooldownService.IsCooldownAsync(operation, msg))
            {
                continue;
            }

            try
            {
                await operation.OnMessageAsync(msg, type);
                _cooldownService.SetCooldown(operation, msg);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "*Error*: {Message}\n```csharp\n{StackTrace}\n```\n```json\n{json}```", exception.Message, exception.StackTrace, JsonSerializer.Serialize(msg, _jsonSerializerOptions));
            }
        }
    }

    private static bool IsAllowed(long? userId, IOperation operation)
    {
        var allowAttr = operation.GetAttribute<AllowAttribute>();
        return allowAttr == null || allowAttr.UserIds.Contains(userId ?? 0);
    }

    public async Task UpdateHandler(Update update)
    {
        foreach (var operation in _operations)
        {
            try
            {
                await operation.OnUpdateAsync(update);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "*Error*: {Message}\n```csharp\n{StackTrace}\n```\n```json\n{json}```", exception.Message, exception.StackTrace, JsonSerializer.Serialize(update, _jsonSerializerOptions));
            }
        }
    }

    public Task ErrorHandler(Exception exception, HandleErrorSource source)
    {
        _logger.LogError(exception, "Telegram polling error. Source: {Source}", source);
        return Task.CompletedTask;
    }
}
