using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Bot.Service.Services.Tools.Utils;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace Saturn.Bot.Service.Services.Tools;

/// <summary>Mutes a chat member for a while at a moderator's request. Created per request.</summary>
public class MuteChatTool : IChatTool
{
    private const int DefaultMinutes = 10;

    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IMessageRepository _messageRepository;
    private readonly ChatToolContext _context;
    private readonly ILogger _logger;
    private bool _muted;

    public MuteChatTool(ITelegramBotClient telegramBotClient, IMessageRepository messageRepository,
        ChatToolContext context, ILogger logger)
    {
        _telegramBotClient = telegramBotClient;
        _messageRepository = messageRepository;
        _context = context;
        _logger = logger;
    }

    public string FunctionName => "mute_user";

    public ChatTool Definition { get; } = ChatTool.CreateFunctionTool(
        functionName: "mute_user",
        functionDescription:
        "Замутить участника чата (запретить писать) на время. Вызывай, когда собеседник просит замутить, " +
        "заткнуть или выдать мут кому-то. " + ChatModeration.AccessNote + " " +
        $"Длительность по умолчанию {DefaultMinutes} минут, если собеседник не назвал другую.",
        functionParameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new Dictionary<string, object>(ChatModeration.TargetProperties("замутить"))
            {
                ["minutes"] = new
                {
                    type = "integer",
                    description =
                        $"На сколько минут мут, по умолчанию {DefaultMinutes}; переведи названный срок в минуты (час — 60, день — 1440)"
                }
            }
        })
    );

    public async Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken)
    {
        var request = _context.Request;
        var accessError = ChatModeration.CheckAccess(_context);
        if (accessError != null)
        {
            _logger.LogInformation("Mute request by {UserId} in chat {ChatId} refused", request.From?.Id,
                request.Chat.Id);
            return accessError;
        }

        if (_muted)
        {
            return ToolArguments.Error("Один мут за ответ");
        }

        using var doc = JsonDocument.Parse(arguments);
        var root = doc.RootElement;

        var minutes = ToolArguments.GetLong(root, "minutes") ?? DefaultMinutes;
        if (minutes is < 1 or > ChatModeration.MaxMinutes)
        {
            return ToolArguments.Error($"Длительность мута — от 1 до {ChatModeration.MaxMinutes} минут");
        }

        var (target, error) =
            await ChatModeration.ResolveTargetAsync(_messageRepository, _context, root, сancellationToken);
        if (target == null)
        {
            return error!;
        }

        var punishError = ChatModeration.CheckPunishable(_context, target);
        if (punishError != null)
        {
            return punishError;
        }

        try
        {
            await _telegramBotClient.RestrictChatMember(
                request.Chat.Id,
                target.UserId,
                new ChatPermissions(false),
                untilDate: DateTime.UtcNow.AddMinutes(minutes),
                cancellationToken: сancellationToken);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to mute user {UserId} in chat {ChatId}", target.UserId, request.Chat.Id);
            return ToolArguments.Error($"Не удалось замутить: {ex.Message}");
        }

        _muted = true;
        _context.MarkActionTaken();
        _logger.LogInformation("Muted user {UserId} in chat {ChatId} for {Minutes} min at request of {RequesterId}",
            target.UserId, request.Chat.Id, minutes, request.From!.Id);
        return ToolArguments.Result(new
        {
            ok = true,
            user_id = target.UserId,
            name = target.Name,
            minutes,
            note = "Мут выдан, коротко сообщи об этом в чат"
        });
    }
}