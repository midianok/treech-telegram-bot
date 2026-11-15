using System.Text.Json;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Services.Tools.Utils;

/// <summary>A chat member a moderation tool acts on.</summary>
public record ModerationTarget(long UserId, string Name);

/// <summary>Shared access checks and target lookup for the moderation tools (mute, unmute, ban, kick).</summary>
public static class ChatModeration
{
    public const int MaxMinutes = 365 * 24 * 60;

    /// <summary>Users who may ask for moderation actions. Never expose this list to the model or the chat.</summary>
    private static readonly HashSet<long> ModeratorIds = [814715436, 198607451];

    public static bool IsModerator(long userId) => ModeratorIds.Contains(userId);

    /// <summary>Error for the model if the requester may not moderate here, otherwise null.</summary>
    public static string? CheckAccess(ChatToolContext context)
    {
        var request = context.Request;
        if (request.From == null || !IsModerator(request.From.Id))
        {
            return ToolArguments.Error("Отказано: по просьбе этого собеседника это не делается. Откажи и не объясняй, кто может");
        }

        if (request.Chat.Type is not (ChatType.Group or ChatType.Supergroup))
        {
            return ToolArguments.Error("Это работает только в групповом чате");
        }

        return null;
    }

    /// <summary>Tool description tail shared by the moderation tools.</summary>
    public const string AccessNote = "Права проверяет сам инструмент — всегда вызывай его, не решай за него. " +
                                     "Если инструмент отказал, просто откажи в своём стиле и никогда не рассказывай и не гадай, " +
                                     "кто может об этом просить. Цель — ответ собеседника (target=replied), user_id или username.";

    /// <summary>JSON schema properties that pick the target member.</summary>
    public static Dictionary<string, object> TargetProperties(string action) => new()
    {
        ["target"] = new
        {
            type = "string",
            @enum = new[] { "replied" },
            description = $"replied — {action} автора сообщения, на которое ответил собеседник"
        },
        ["user_id"] = new
        {
            type = "integer",
            description = "Telegram ID участника"
        },
        ["username"] = new
        {
            type = "string",
            description = "Username, имя или фамилия участника, например «@vasya» или «Дима». " +
                          "Если подходят несколько участников, вернётся список кандидатов — уточни у собеседника"
        }
    };

    /// <summary>Finds the target member from the tool arguments; on failure returns an error for the model instead.</summary>
    public static async Task<(ModerationTarget? Target, string? Error)> ResolveTargetAsync(
        IMessageRepository messageRepository,
        ChatToolContext context,
        JsonElement root,
        CancellationToken сancellationToken)
    {
        var request = context.Request;
        var userId = ToolArguments.GetLong(root, "user_id");
        var username = ToolArguments.GetString(root, "username");

        if (userId == null && username == null)
        {
            if (ToolArguments.GetString(root, "target") != "replied")
            {
                return (null, ToolArguments.Error("Укажи участника: target=replied, user_id или username"));
            }

            var replied = request.ReplyToMessage?.From;
            if (replied == null)
            {
                return (null, ToolArguments.Error("Собеседник ни на что не отвечал — уточни, о ком речь"));
            }
            if (replied.IsBot)
            {
                return (null, ToolArguments.Error("С ботами так нельзя"));
            }
            return (new ModerationTarget(replied.Id, $"{replied.FirstName} {replied.LastName}".Trim()), null);
        }

        var chatAuthors = await messageRepository.GetChatAuthorsAsync(request.Chat.Id, сancellationToken);
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
                    note = "Под запрос подходят несколько участников. Спроси у собеседника, кого он имел в виду, перечислив варианты",
                    candidates = candidates.Select(a => new { user_id = a.UserId, username = a.Username, name = $"{a.FirstName} {a.LastName}".Trim() })
                }));
            }
            member ??= candidates.FirstOrDefault();
        }

        if (member == null)
        {
            return (null, ToolArguments.Error($"В чате нет участника «{(object?)userId ?? username}», уточни у собеседника"));
        }

        return (new ModerationTarget(member.UserId, $"{member.FirstName} {member.LastName}".Trim()), null);
    }

    /// <summary>Error if the target is the requester or another moderator, who are off limits for punishments.</summary>
    public static string? CheckPunishable(ChatToolContext context, ModerationTarget target) =>
        target.UserId == context.Request.From!.Id || IsModerator(target.UserId)
            ? ToolArguments.Error("Этого участника трогать нельзя")
            : null;
}
