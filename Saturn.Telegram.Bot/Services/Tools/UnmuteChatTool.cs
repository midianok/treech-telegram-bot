using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Bot.Service.Services.Tools.Utils;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Saturn.Bot.Service.Services.Prompts;

namespace Saturn.Bot.Service.Services.Tools;

/// <summary>Lifts a mute early at a moderator's request. Created per request.</summary>
public class UnmuteChatTool : IChatTool
{
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IMessageRepository _messageRepository;
    private readonly ChatToolContext _context;
    private readonly ILogger _logger;
    private bool _unmuted;

    public UnmuteChatTool(ITelegramBotClient telegramBotClient, IMessageRepository messageRepository, ChatToolContext context, ILogger logger)
    {
        _telegramBotClient = telegramBotClient;
        _messageRepository = messageRepository;
        _context = context;
        _logger = logger;
    }

    public string FunctionName => "unmute_user";

    public ChatTool Definition { get; } = PromptLibrary.Tool("unmute_user", new
    {
        type = "object",
        properties = ChatModeration.TargetProperties
    }, new { action = "размутить" });

    public async Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken)
    {
        var request = _context.Request;
        var accessError = ChatModeration.CheckAccess(_context);
        if (accessError != null)
        {
            _logger.LogInformation("Unmute request by {UserId} in chat {ChatId} refused", request.From?.Id, request.Chat.Id);
            return accessError;
        }

        if (_unmuted)
        {
            return ToolArguments.Error("Один размут за ответ");
        }

        using var doc = JsonDocument.Parse(arguments);
        var (target, error) = await ChatModeration.ResolveTargetAsync(_messageRepository, _context, doc.RootElement, сancellationToken);
        if (target == null)
        {
            return error!;
        }

        try
        {
            // Give back what regular members of this chat may do, not more
            var chat = await _telegramBotClient.GetChat(request.Chat.Id, сancellationToken);
            await _telegramBotClient.RestrictChatMember(
                request.Chat.Id,
                target.UserId,
                chat.Permissions ?? new ChatPermissions(true),
                cancellationToken: сancellationToken);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to unmute user {UserId} in chat {ChatId}", target.UserId, request.Chat.Id);
            return ToolArguments.Error($"Не удалось размутить: {ex.Message}");
        }

        _unmuted = true;
        _context.MarkActionTaken();
        _logger.LogInformation("Unmuted user {UserId} in chat {ChatId} at request of {RequesterId}", target.UserId, request.Chat.Id, request.From!.Id);
        return ToolArguments.Result(new
        {
            ok = true,
            user_id = target.UserId,
            name = target.Name,
            note = "Мут снят, коротко сообщи об этом в чат"
        });
    }
}
