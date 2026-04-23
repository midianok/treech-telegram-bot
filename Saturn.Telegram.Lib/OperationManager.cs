using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Infrastructure;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Lib;

public class OperationManager
{
    private readonly IEnumerable<IOperation> _operations;
    private readonly ILogger<OperationManager> _logger;
    private readonly ICooldownService _cooldownService;
    private readonly IOperationCallRepository _operationCallRepository;
    private readonly ISaveMessageService _saveMessageService;
    private readonly TelegramBotClient _botClient;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public OperationManager(
        IEnumerable<IOperation> operations,
        ILogger<OperationManager> logger,
        ICooldownService cooldownService,
        IOperationCallRepository operationCallRepository,
        ISaveMessageService saveMessageService,
        TelegramBotClient botClient)
    {
        _operations = operations;
        _logger = logger;
        _cooldownService = cooldownService;
        _operationCallRepository = operationCallRepository;
        _saveMessageService = saveMessageService;
        _botClient = botClient;
    }

    public async Task MessageHandler(Message msg, UpdateType type)
    {
        await _saveMessageService.SaveMessageAsync(msg);

        foreach (var operation in _operations)
        {
            if (operation.IsIgnored()) continue;
            
            if (!operation.Validate(msg, type)) continue;

            if (!operation.IsAllowed(msg.From?.Id)) continue;

            if (await operation.IsChatOnlyViolatedAsync(msg, _botClient)) continue;

            if (await _cooldownService.IsCooldownAsync(operation, msg)) continue;

            try
            {
                await operation.OnMessageAsync(msg, type);
                _cooldownService.SetCooldown(operation, msg);
                await _operationCallRepository.RecordAsync(operation.GetType().Name, msg.Chat.Id, msg.From?.Id ?? 0);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "*Error*: {Message}\n```csharp\n{StackTrace}\n```\n```json\n{json}```", exception.Message, exception.StackTrace, JsonSerializer.Serialize(msg, _jsonSerializerOptions));
            }
        }
    }

    public async Task UpdateHandler(Update update)
    {
        foreach (var operation in _operations)
        {
            if (operation.IsIgnored())
            {
                continue;
            }

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
