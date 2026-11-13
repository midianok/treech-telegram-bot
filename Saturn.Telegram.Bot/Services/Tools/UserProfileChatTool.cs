using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Saturn.Telegram.Lib.Extensions;
using Telegram.Bot;

namespace Saturn.Bot.Service.Services.Tools;

/// <summary>
/// Reads the Telegram profile (bio and avatar) of a member of the chat the request came from.
/// Bound to a single chat, so it is created per request instead of being registered in DI.
/// </summary>
public class UserProfileChatTool : IChatTool
{
    private const string AvatarPrompt =
        "Это аватарка пользователя Telegram. Опиши, что на ней изображено: кто или что, внешность, одежда, поза, " +
        "выражение лица, фон, предметы, надписи, стиль изображения и общая атмосфера. " +
        "Отметь самые характерные и необычные детали. Описывай только то, что видно, ничего не придумывай. " +
        "Затем кратко предположи, что это может говорить о владельце: увлечения, вкусы, как он хочет себя подать " +
        "(подавай это как предположения, а не факты). Без разметки, по-русски, 3–5 предложений.";

    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public string FunctionName => "get_user_profile";

    public ChatTool Definition { get; } = ChatTool.CreateFunctionTool(
        functionName: "get_user_profile",
        functionDescription: "Получить профиль Telegram участника текущего чата: описание профиля (bio) и описание аватарки. " +
                             "Описание и аватарка возвращаются, только если пользователь их не скрыл настройками приватности. " +
                             "Используй, когда спрашивают про чей-то профиль, био или аватарку. " +
                             "Обязательно вызывай, когда просят подколоть, прожарить, описать или оценить человека, " +
                             "вместе с поиском по истории чата: это дополнительные знания о нём (увлечения, вкусы, образ, " +
                             "как он себя подаёт). Используй их как то, что ты и так о нём знаешь: не пересказывай аватарку " +
                             "или описание профиля и не упоминай их («на аватарке ты…», «в био написано…»). " +
                             "Укажи user_id или username.",
        functionParameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                user_id = new
                {
                    type = "integer",
                    description = "Telegram ID участника"
                },
                username = new
                {
                    type = "string",
                    description = "Username, имя или фамилия участника, например «@vasya» или «Дима». " +
                                  "Если подходят несколько участников, вернётся список кандидатов — " +
                                  "уточни у собеседника, кого он имел в виду, или повтори запрос с user_id нужного"
                }
            }
        })
    );

    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IAiService _aiService;
    private readonly IMessageRepository _messageRepository;
    private readonly long _chatId;
    private readonly ILogger<UserProfileChatTool> _logger;

    public UserProfileChatTool(
        ITelegramBotClient telegramBotClient,
        IAiService aiService,
        IMessageRepository messageRepository,
        long chatId,
        ILogger<UserProfileChatTool> logger)
    {
        _telegramBotClient = telegramBotClient;
        _aiService = aiService;
        _messageRepository = messageRepository;
        _chatId = chatId;
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken)
    {
        using var doc = JsonDocument.Parse(arguments);
        var root = doc.RootElement;

        long? userId = root.TryGetProperty("user_id", out var userIdElement) && userIdElement.TryGetInt64(out var parsedUserId)
            ? parsedUserId
            : null;
        var username = root.TryGetProperty("username", out var usernameElement) && usernameElement.ValueKind == JsonValueKind.String
            ? usernameElement.GetString()
            : null;

        // Only members of this chat: the tool must not become a way to look up arbitrary Telegram users
        var chatAuthors = await _messageRepository.GetChatAuthorsAsync(_chatId, сancellationToken);
        ChatAuthor? member;
        if (userId != null)
        {
            member = chatAuthors.FirstOrDefault(a => a.UserId == userId);
        }
        else if (!string.IsNullOrWhiteSpace(username))
        {
            var normalizedUsername = username.Trim().TrimStart('@');
            var candidates = ChatHistoryChatTool.FindAuthors(chatAuthors, username);
            member = candidates.FirstOrDefault(a => string.Equals(a.Username, normalizedUsername, StringComparison.OrdinalIgnoreCase));
            if (member == null && candidates.Count > 1)
            {
                return JsonSerializer.Serialize(new
                {
                    ambiguous_user = true,
                    note = "Под запрос подходят несколько участников. Спроси у собеседника, кого он имел в виду, перечислив варианты",
                    candidates = candidates.Select(a => new { user_id = a.UserId, username = a.Username, name = $"{a.FirstName} {a.LastName}".Trim() })
                }, JsonOptions);
            }
            member ??= candidates.FirstOrDefault();
        }
        else
        {
            return """{"error":"Укажи user_id или username"}""";
        }

        if (member == null)
        {
            return JsonSerializer.Serialize(new
            {
                error = $"В чате нет участника «{(object?)userId ?? username}». Не подставляй профиль другого человека, уточни у собеседника"
            }, JsonOptions);
        }

        _logger.LogInformation("Reading profile of user {UserId} (@{Username}) in chat {ChatId}", member.UserId, member.Username, _chatId);

        var bioTask = GetBioAsync(member.UserId, сancellationToken);
        var avatarTask = DescribeAvatarAsync(member.UserId, сancellationToken);
        await Task.WhenAll(bioTask, avatarTask);

        return JsonSerializer.Serialize(new
        {
            user_id = member.UserId,
            username = member.Username,
            name = $"{member.FirstName} {member.LastName}".Trim(),
            bio = bioTask.Result ?? "нет или скрыто",
            avatar = avatarTask.Result ?? "нет или скрыта"
        }, JsonOptions);
    }

    private async Task<string?> GetBioAsync(long userId, CancellationToken сancellationToken)
    {
        try
        {
            var chat = await _telegramBotClient.GetChat(userId, сancellationToken);
            return string.IsNullOrWhiteSpace(chat.Bio) ? null : chat.Bio;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Telegram refuses getChat for users the bot can't see; that's the same as a hidden bio for the model
            _logger.LogWarning(exception, "Failed to get bio of user {UserId}", userId);
            return null;
        }
    }

    private async Task<string?> DescribeAvatarAsync(long userId, CancellationToken сancellationToken)
    {
        try
        {
            var photos = await _telegramBotClient.GetUserProfilePhotos(userId, limit: 1, cancellationToken: сancellationToken);
            var fileId = photos.Photos.FirstOrDefault()?.MaxBy(x => x.FileSize)?.FileId;
            if (string.IsNullOrEmpty(fileId))
            {
                return null;
            }

            var fileData = await _telegramBotClient.DownloadFileAsync(fileId, сancellationToken);
            return await _aiService.CompleteChatAsync(
            [
                new UserChatMessage(
                    ChatMessageContentPart.CreateImagePart(new BinaryData(fileData), "image/jpeg", ChatImageDetailLevel.Auto),
                    ChatMessageContentPart.CreateTextPart(AvatarPrompt))
            ], сancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Failed to describe avatar of user {UserId}", userId);
            return null;
        }
    }
}
