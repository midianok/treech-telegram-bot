using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Bot.Service.Services.Tools.Utils;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Services.Tools;

/// <summary>Mutes a chat member for a while at a moderator's request. Created per request.</summary>
public class MuteChatTool : IChatTool
{
    private const int DefaultMinutes = 10;
    private const int MaxMinutes = 365 * 24 * 60;

    /// <summary>Users who may ask for a mute. Never expose this list to the model or the chat.</summary>
    private static readonly HashSet<long> ModeratorIds = [814715436, 198607451];

    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IMessageRepository _messageRepository;
    private readonly ChatToolContext _context;
    private readonly ILogger _logger;
    private bool _muted;

    public MuteChatTool(ITelegramBotClient telegramBotClient, IMessageRepository messageRepository, ChatToolContext context, ILogger logger)
    {
        _telegramBotClient = telegramBotClient;
        _messageRepository = messageRepository;
        _context = context;
        _logger = logger;
    }

    public string FunctionName => "mute_user";

    public ChatTool Definition { get; } = ChatTool.CreateFunctionTool(
        functionName: "mute_user",
        functionDescription: "Замутить участника чата (запретить писать) на время. Вызывай, когда собеседник просит замутить, " +
                             "заткнуть или выдать мут кому-то. Права проверяет сам инструмент — всегда вызывай его, не решай за него. " +
                             "Если инструмент отказал, просто откажи в своём стиле и никогда не рассказывай и не гадай, " +
                             "кто может просить мут. Цель — ответ собеседника (target=replied), user_id или username. " +
                             $"Длительность по умолчанию {DefaultMinutes} минут, если собеседник не назвал другую.",
        functionParameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                target = new
                {
                    type = "string",
                    @enum = new[] { "replied" },
                    description = "replied — замутить автора сообщения, на которое ответил собеседник"
                },
                user_id = new
                {
                    type = "integer",
                    description = "Telegram ID участника"
                },
                username = new
                {
                    type = "string",
                    description = "Username, имя или фамилия участника, например «@vasya» или «Дима». " +
                                  "Если подходят несколько участников, вернётся список кандидатов — уточни у собеседника"
                },
                minutes = new
                {
                    type = "integer",
                    description = $"На сколько минут мут, по умолчанию {DefaultMinutes}; переведи названный срок в минуты (час — 60, день — 1440)"
                }
            }
        })
    );

    public async Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken)
    {
        var request = _context.Request;
        if (request.From == null || !ModeratorIds.Contains(request.From.Id))
        {
            _logger.LogInformation("Mute request by {UserId} in chat {ChatId} refused", request.From?.Id, request.Chat.Id);
            return ToolArguments.Error("Отказано: по просьбе этого собеседника мут не выдаётся. Откажи и не объясняй, кто может");
        }

        if (request.Chat.Type is not (ChatType.Group or ChatType.Supergroup))
        {
            return ToolArguments.Error("Мутить можно только в групповом чате");
        }

        if (_muted)
        {
            return ToolArguments.Error("Один мут за ответ");
        }

        using var doc = JsonDocument.Parse(arguments);
        var root = doc.RootElement;

        var minutes = ToolArguments.GetLong(root, "minutes") ?? DefaultMinutes;
        if (minutes is < 1 or > MaxMinutes)
        {
            return ToolArguments.Error($"Длительность мута — от 1 до {MaxMinutes} минут");
        }

        var (target, error) = await ResolveTargetAsync(root, сancellationToken);
        if (target == null)
        {
            return error!;
        }

        if (target.Value.UserId == request.From.Id || ModeratorIds.Contains(target.Value.UserId))
        {
            return ToolArguments.Error("Этого участника мутить нельзя");
        }

        var until = DateTime.UtcNow.AddMinutes(minutes);
        try
        {
            await _telegramBotClient.RestrictChatMember(
                request.Chat.Id,
                target.Value.UserId,
                new ChatPermissions(false),
                untilDate: until,
                cancellationToken: сancellationToken);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to mute user {UserId} in chat {ChatId}", target.Value.UserId, request.Chat.Id);
            return ToolArguments.Error($"Не удалось замутить: {ex.Message}");
        }

        _muted = true;
        _context.MarkActionTaken();
        _logger.LogInformation("Muted user {UserId} in chat {ChatId} for {Minutes} min at request of {RequesterId}",
            target.Value.UserId, request.Chat.Id, minutes, request.From.Id);
        return ToolArguments.Result(new
        {
            ok = true,
            user_id = target.Value.UserId,
            name = target.Value.Name,
            minutes,
            note = "Мут выдан, коротко сообщи об этом в чат"
        });
    }

    private async Task<((long UserId, string Name)? Target, string? Error)> ResolveTargetAsync(JsonElement root, CancellationToken сancellationToken)
    {
        var request = _context.Request;
        var userId = ToolArguments.GetLong(root, "user_id");
        var username = ToolArguments.GetString(root, "username");

        if (userId == null && username == null)
        {
            if (ToolArguments.GetString(root, "target") != "replied")
            {
                return (null, ToolArguments.Error("Укажи, кого мутить: target=replied, user_id или username"));
            }

            var replied = request.ReplyToMessage?.From;
            if (replied == null)
            {
                return (null, ToolArguments.Error("Собеседник ни на что не отвечал — уточни, кого мутить"));
            }
            if (replied.IsBot)
            {
                return (null, ToolArguments.Error("Ботов мутить нельзя"));
            }
            return ((replied.Id, $"{replied.FirstName} {replied.LastName}".Trim()), null);
        }

        var chatAuthors = await _messageRepository.GetChatAuthorsAsync(request.Chat.Id, сancellationToken);
        ChatAuthor? member;
        if (userId != null)
        {
            member = chatAuthors.FirstOrDefault(a => a.UserId == userId);
        }
        else
        {
            var candidates = ChatAuthorMatcher.Find(chatAuthors, username!);
            member = ChatAuthorMatcher.FindExactUsername(candidates, username!);
            if (member == null && candidates.Count > 1)
            {
                return (null, ToolArguments.Result(new
                {
                    ambiguous_user = true,
                    note = "Под запрос подходят несколько участников. Спроси у собеседника, кого замутить, перечислив варианты",
                    candidates = candidates.Select(a => new { user_id = a.UserId, username = a.Username, name = $"{a.FirstName} {a.LastName}".Trim() })
                }));
            }
            member ??= candidates.FirstOrDefault();
        }

        if (member == null)
        {
            return (null, ToolArguments.Error($"В чате нет участника «{(object?)userId ?? username}», уточни у собеседника"));
        }

        return ((member.UserId, $"{member.FirstName} {member.LastName}".Trim()), null);
    }
}
