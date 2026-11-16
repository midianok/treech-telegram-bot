using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Bot.Service.Services.Tools.Utils;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Saturn.Bot.Service.Services.Prompts;

namespace Saturn.Bot.Service.Services.Tools;

/// <summary>Removes a member from the chat without a ban at a moderator's request. Created per request.</summary>
public class KickChatTool : IChatTool
{
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IMessageRepository _messageRepository;
    private readonly ChatToolContext _context;
    private readonly ILogger _logger;
    private bool _kicked;

    public KickChatTool(ITelegramBotClient telegramBotClient, IMessageRepository messageRepository, ChatToolContext context, ILogger logger)
    {
        _telegramBotClient = telegramBotClient;
        _messageRepository = messageRepository;
        _context = context;
        _logger = logger;
    }

    public string FunctionName => "kick_user";

    public ChatTool Definition { get; } = PromptLibrary.Tool("kick_user", new
    {
        type = "object",
        properties = ChatModeration.TargetProperties
    }, new { action = "выгнать" });

    public async Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken)
    {
        var request = _context.Request;
        var accessError = ChatModeration.CheckAccess(_context);
        if (accessError != null)
        {
            _logger.LogInformation("Kick request by {UserId} in chat {ChatId} refused", request.From?.Id, request.Chat.Id);
            return accessError;
        }

        if (_kicked)
        {
            return ToolArguments.Error("Один кик за ответ");
        }

        using var doc = JsonDocument.Parse(arguments);
        var (target, error) = await ChatModeration.ResolveTargetAsync(_messageRepository, _context, doc.RootElement, сancellationToken);
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
            // Telegram has no kick: ban removes the member, the unban right after lets them rejoin
            await _telegramBotClient.BanChatMember(request.Chat.Id, target.UserId, cancellationToken: сancellationToken);
            await _telegramBotClient.UnbanChatMember(request.Chat.Id, target.UserId, onlyIfBanned: true, cancellationToken: сancellationToken);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to kick user {UserId} from chat {ChatId}", target.UserId, request.Chat.Id);
            return ToolArguments.Error($"Не удалось выгнать: {ex.Message}");
        }

        _kicked = true;
        _context.MarkActionTaken();
        _logger.LogInformation("Kicked user {UserId} from chat {ChatId} at request of {RequesterId}", target.UserId, request.Chat.Id, request.From!.Id);
        return ToolArguments.Result(new
        {
            ok = true,
            user_id = target.UserId,
            name = target.Name,
            note = "Участник выгнан, коротко сообщи об этом в чат"
        });
    }
}
