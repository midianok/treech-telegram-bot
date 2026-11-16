using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Bot.Service.Services.Prompts;
using Saturn.Bot.Service.Services.Tools.Utils;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace Saturn.Bot.Service.Services.Tools;

/// <summary>
/// Mutes, unmutes, bans, unbans or kicks a chat member at a moderator's request.
/// Created per request and offered to the model only when the requester is a moderator.
/// </summary>
public class ModerationChatTool : IChatTool
{
    private const int DefaultMuteMinutes = 10;

    private const string Mute = "mute";
    private const string Unmute = "unmute";
    private const string Ban = "ban";
    private const string Unban = "unban";
    private const string Kick = "kick";

    private static readonly ChatTool ToolDefinition = PromptLibrary.Tool("moderate_user", new
    {
        type = "object",
        properties = new Dictionary<string, object>(ChatModeration.TargetProperties)
        {
            ["action"] = new { type = "string", @enum = new[] { Mute, Unmute, Ban, Unban, Kick } },
            ["minutes"] = new { type = "integer" }
        },
        required = new[] { "action" }
    }, new { defaultMinutes = DefaultMuteMinutes });

    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IMessageRepository _messageRepository;
    private readonly ChatToolContext _context;
    private readonly ILogger _logger;
    private readonly HashSet<string> _doneActions = [];

    public ModerationChatTool(ITelegramBotClient telegramBotClient, IMessageRepository messageRepository,
        ChatToolContext context, ILogger logger)
    {
        _telegramBotClient = telegramBotClient;
        _messageRepository = messageRepository;
        _context = context;
        _logger = logger;
    }

    public string FunctionName => "moderate_user";

    public ChatTool Definition => ToolDefinition;

    public async Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken)
    {
        var request = _context.Request;
        using var doc = JsonDocument.Parse(arguments);
        var root = doc.RootElement;
        var action = ToolArguments.GetString(root, "action");

        var accessError = ChatModeration.CheckAccess(_context);
        if (accessError != null)
        {
            _logger.LogInformation("Moderation {Action} by {UserId} in chat {ChatId} refused", action, request.From?.Id,
                request.Chat.Id);
            return accessError;
        }

        if (action is not (Mute or Unmute or Ban or Unban or Kick))
        {
            return ToolArguments.Error("Укажи action: mute, unmute, ban, unban или kick");
        }

        if (_doneActions.Contains(action))
        {
            return ToolArguments.Error("Одно такое действие за ответ");
        }

        var minutes = ToolArguments.GetLong(root, "minutes");
        if (action == Mute)
        {
            minutes ??= DefaultMuteMinutes;
        }
        if ((action is Mute or Ban) && minutes is < 1 or > ChatModeration.MaxMinutes)
        {
            return ToolArguments.Error($"Длительность — от 1 до {ChatModeration.MaxMinutes} минут");
        }

        var (target, error) = await ChatModeration.ResolveTargetAsync(_messageRepository, _context, root, сancellationToken);
        if (target == null)
        {
            return error!;
        }

        if (action is Mute or Ban or Kick)
        {
            var punishError = ChatModeration.CheckPunishable(_context, target);
            if (punishError != null)
            {
                return punishError;
            }
        }

        string note;
        try
        {
            note = await ApplyAsync(action, target, minutes, сancellationToken);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogWarning(ex, "Moderation {Action} of user {UserId} in chat {ChatId} failed", action, target.UserId,
                request.Chat.Id);
            return ToolArguments.Error($"Не получилось: {ex.Message}");
        }

        _doneActions.Add(action);
        _context.MarkActionTaken();
        _logger.LogInformation("Moderation {Action} of user {UserId} in chat {ChatId} for {Minutes} min at request of {RequesterId}",
            action, target.UserId, request.Chat.Id, minutes?.ToString() ?? "-", request.From!.Id);
        return ToolArguments.Result(new
        {
            ok = true,
            action,
            user_id = target.UserId,
            name = target.Name,
            minutes = action switch
            {
                Mute => minutes,
                Ban => (object?)minutes ?? "навсегда",
                _ => null
            },
            note
        });
    }

    /// <summary>Performs the action in Telegram and returns the note for the model.</summary>
    private async Task<string> ApplyAsync(string action, ModerationTarget target, long? minutes, CancellationToken сancellationToken)
    {
        var chatId = _context.Request.Chat.Id;
        switch (action)
        {
            case Mute:
            {
                await _telegramBotClient.RestrictChatMember(chatId, target.UserId, new ChatPermissions(false),
                    untilDate: DateTime.UtcNow.AddMinutes(minutes!.Value), cancellationToken: сancellationToken);
                return "Мут выдан, коротко сообщи об этом в чат";
            }
            case Unmute:
            {
                // Give back what regular members of this chat may do, not more
                var chat = await _telegramBotClient.GetChat(chatId, сancellationToken);
                await _telegramBotClient.RestrictChatMember(chatId, target.UserId, chat.Permissions ?? new ChatPermissions(true),
                    cancellationToken: сancellationToken);
                return "Мут снят, коротко сообщи об этом в чат";
            }
            case Ban:
            {
                await _telegramBotClient.BanChatMember(chatId, target.UserId,
                    untilDate: minutes == null ? null : DateTime.UtcNow.AddMinutes(minutes.Value),
                    cancellationToken: сancellationToken);
                return "Бан выдан, коротко сообщи об этом в чат";
            }
            case Unban:
            {
                await _telegramBotClient.UnbanChatMember(chatId, target.UserId, onlyIfBanned: true,
                    cancellationToken: сancellationToken);
                return "Бан снят, коротко сообщи об этом в чат: вернуться он может по ссылке-приглашению";
            }
            default:
            {
                // Telegram has no kick: ban removes the member, the unban right after lets them rejoin
                await _telegramBotClient.BanChatMember(chatId, target.UserId, cancellationToken: сancellationToken);
                await _telegramBotClient.UnbanChatMember(chatId, target.UserId, onlyIfBanned: true,
                    cancellationToken: сancellationToken);
                return "Участник выгнан, коротко сообщи об этом в чат";
            }
        }
    }
}
