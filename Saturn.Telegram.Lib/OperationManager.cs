using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Saturn.Telegram.Lib.Abstractions;
using Saturn.Telegram.Lib.Exceptions;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Infrastructure.Abstractions;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Lib;

public class OperationManager : IOperationManager
{
    private const int MaxLoggedTextLength = 200;

    private readonly IEnumerable<IOperation> _operations;
    private readonly ILogger<OperationManager> _logger;
    private readonly ICooldownService _cooldownService;
    private readonly IOperationCallRepository _operationCallRepository;
    private readonly ISaveMessageService _saveMessageService;
    private readonly ITelegramBotClient _botClient;
    private readonly string? _adminUsername;

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
        ITelegramBotClient botClient,
        IConfiguration configuration)
    {
        _operations = operations;
        _logger = logger;
        _cooldownService = cooldownService;
        _operationCallRepository = operationCallRepository;
        _saveMessageService = saveMessageService;
        _botClient = botClient;
        _adminUsername = configuration["ADMIN_USERNAME"];
    }

    public async Task MessageHandler(Message msg, UpdateType type, CancellationToken сancellationToken)
    {
        await _saveMessageService.SaveMessageAsync(msg);

        foreach (var operation in _operations)
        {
            if (operation.IsIgnored()) continue;

            if (!operation.Validate(msg, type)) continue;

            if (!operation.IsAllowed(msg.From?.Username)) continue;

            if (await operation.IsChatOnlyViolatedAsync(msg, _botClient, _adminUsername)) continue;

            var operationName = operation.GetType().Name;
            var user = msg.From?.Username ?? msg.From?.Id.ToString() ?? "unknown";

            if (await _cooldownService.IsCooldownAsync(operation, msg))
            {
                _logger.LogInformation("Operation {Operation} skipped by cooldown: chat {ChatId}, user {User}", operationName, msg.Chat.Id, user);
                continue;
            }

            _logger.LogInformation(
                "Operation {Operation} started: chat {ChatId} ({ChatTitle}), user {User}, message {MessageId}, text: {Text}",
                operationName, msg.Chat.Id, msg.Chat.Title ?? "private", user, msg.Id, Truncate(msg.Text ?? msg.Caption));

            var stopwatch = Stopwatch.StartNew();

            try
            {
                await operation.OnMessageAsync(msg, type, сancellationToken);
                _cooldownService.SetCooldown(operation, msg);
                await _operationCallRepository.RecordAsync(operationName, msg.Chat.Id, msg.From?.Id ?? 0);

                _logger.LogInformation("Operation {Operation} completed in {Elapsed:F1} s", operationName, stopwatch.Elapsed.TotalSeconds);
            }
            catch (AiBudgetExhaustedException)
            {
                _logger.LogInformation("Operation {Operation} stopped after {Elapsed:F1} s: AI budget exhausted", operationName, stopwatch.Elapsed.TotalSeconds);
                await _botClient.SendMessage(msg.Chat, "денег нет, но вы держитесь", replyParameters: new ReplyParameters { MessageId = msg.Id });
            }
            catch (AiContentModerationException)
            {
                _logger.LogInformation("Operation {Operation} stopped after {Elapsed:F1} s: content moderation rejection", operationName, stopwatch.Elapsed.TotalSeconds);
                await _botClient.SendMessage(msg.Chat, "запрос не прошёл модерацию контента", replyParameters: new ReplyParameters { MessageId = msg.Id });
            }
            catch (AiEmptyResponseException)
            {
                _logger.LogInformation("Operation {Operation} stopped after {Elapsed:F1} s: empty AI response", operationName, stopwatch.Elapsed.TotalSeconds);
                await _botClient.SendMessage(msg.Chat, "что-то пошло не так", replyParameters: new ReplyParameters { MessageId = msg.Id });
            }
            catch (Exception exception)
            {
                _logger.LogInformation("Operation {Operation} failed after {Elapsed:F1} s", operationName, stopwatch.Elapsed.TotalSeconds);
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

    private static string Truncate(string? text) =>
        text switch
        {
            null => string.Empty,
            { Length: > MaxLoggedTextLength } => text[..MaxLoggedTextLength] + "…",
            _ => text
        };

    public Task ErrorHandler(Exception exception, HandleErrorSource source)
    {
        _logger.LogError(exception, "Telegram polling error. Source: {Source}", source);
        return Task.CompletedTask;
    }
}
