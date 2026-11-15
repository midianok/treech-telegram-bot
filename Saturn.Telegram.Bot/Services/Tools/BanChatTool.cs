using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Bot.Service.Services.Tools.Utils;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;

namespace Saturn.Bot.Service.Services.Tools;

/// <summary>Bans a chat member, forever or for a while, at a moderator's request. Created per request.</summary>
public class BanChatTool : IChatTool
{
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IMessageRepository _messageRepository;
    private readonly ChatToolContext _context;
    private readonly ILogger _logger;
    private bool _banned;

    public BanChatTool(ITelegramBotClient telegramBotClient, IMessageRepository messageRepository, ChatToolContext context, ILogger logger)
    {
        _telegramBotClient = telegramBotClient;
        _messageRepository = messageRepository;
        _context = context;
        _logger = logger;
    }

    public string FunctionName => "ban_user";

    public ChatTool Definition { get; } = ChatTool.CreateFunctionTool(
        functionName: "ban_user",
        functionDescription: "Забанить участника чата: он вылетит из чата и не сможет вернуться, пока бан не кончится. " +
                             "Вызывай, когда собеседник просит забанить кого-то. Если просят просто выгнать — это kick_user. " +
                             ChatModeration.AccessNote + " Без срока бан навсегда.",
        functionParameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new Dictionary<string, object>(ChatModeration.TargetProperties("забанить"))
            {
                ["minutes"] = new
                {
                    type = "integer",
                    description = "На сколько минут бан, если собеседник назвал срок (час — 60, день — 1440); без срока не указывай — бан навсегда"
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
            _logger.LogInformation("Ban request by {UserId} in chat {ChatId} refused", request.From?.Id, request.Chat.Id);
            return accessError;
        }

        if (_banned)
        {
            return ToolArguments.Error("Один бан за ответ");
        }

        using var doc = JsonDocument.Parse(arguments);
        var root = doc.RootElement;

        var minutes = ToolArguments.GetLong(root, "minutes");
        if (minutes is < 1 or > ChatModeration.MaxMinutes)
        {
            return ToolArguments.Error($"Длительность бана — от 1 до {ChatModeration.MaxMinutes} минут, или не указывай её для вечного бана");
        }

        var (target, error) = await ChatModeration.ResolveTargetAsync(_messageRepository, _context, root, сancellationToken);
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
            await _telegramBotClient.BanChatMember(
                request.Chat.Id,
                target.UserId,
                untilDate: minutes == null ? null : DateTime.UtcNow.AddMinutes(minutes.Value),
                cancellationToken: сancellationToken);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to ban user {UserId} in chat {ChatId}", target.UserId, request.Chat.Id);
            return ToolArguments.Error($"Не удалось забанить: {ex.Message}");
        }

        _banned = true;
        _context.MarkActionTaken();
        _logger.LogInformation("Banned user {UserId} in chat {ChatId} for {Minutes} min at request of {RequesterId}",
            target.UserId, request.Chat.Id, minutes?.ToString() ?? "forever", request.From!.Id);
        return ToolArguments.Result(new
        {
            ok = true,
            user_id = target.UserId,
            name = target.Name,
            minutes = (object?)minutes ?? "навсегда",
            note = "Бан выдан, коротко сообщи об этом в чат"
        });
    }
}
