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

/// <summary>Lifts a ban at a moderator's request, so the member can rejoin. Created per request.</summary>
public class UnbanChatTool : IChatTool
{
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IMessageRepository _messageRepository;
    private readonly ChatToolContext _context;
    private readonly ILogger _logger;
    private bool _unbanned;

    public UnbanChatTool(ITelegramBotClient telegramBotClient, IMessageRepository messageRepository, ChatToolContext context, ILogger logger)
    {
        _telegramBotClient = telegramBotClient;
        _messageRepository = messageRepository;
        _context = context;
        _logger = logger;
    }

    public string FunctionName => "unban_user";

    public ChatTool Definition { get; } = PromptLibrary.Tool("unban_user", new
    {
        type = "object",
        properties = ChatModeration.TargetProperties
    }, new { action = "разбанить" });

    public async Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken)
    {
        var request = _context.Request;
        var accessError = ChatModeration.CheckAccess(_context);
        if (accessError != null)
        {
            _logger.LogInformation("Unban request by {UserId} in chat {ChatId} refused", request.From?.Id, request.Chat.Id);
            return accessError;
        }

        if (_unbanned)
        {
            return ToolArguments.Error("Один разбан за ответ");
        }

        using var doc = JsonDocument.Parse(arguments);
        var (target, error) = await ChatModeration.ResolveTargetAsync(_messageRepository, _context, doc.RootElement, сancellationToken);
        if (target == null)
        {
            return error!;
        }

        try
        {
            await _telegramBotClient.UnbanChatMember(request.Chat.Id, target.UserId, onlyIfBanned: true, cancellationToken: сancellationToken);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to unban user {UserId} in chat {ChatId}", target.UserId, request.Chat.Id);
            return ToolArguments.Error($"Не удалось разбанить: {ex.Message}");
        }

        _unbanned = true;
        _context.MarkActionTaken();
        _logger.LogInformation("Unbanned user {UserId} in chat {ChatId} at request of {RequesterId}", target.UserId, request.Chat.Id, request.From!.Id);
        return ToolArguments.Result(new
        {
            ok = true,
            user_id = target.UserId,
            name = target.Name,
            note = "Бан снят, коротко сообщи об этом в чат: вернуться он может по ссылке-приглашению"
        });
    }
}
