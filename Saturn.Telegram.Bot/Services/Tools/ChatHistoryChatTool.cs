using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Db.Repositories.Abstractions;

namespace Saturn.Bot.Service.Services.Tools;

/// <summary>
/// Searches the history of the chat the request came from. Bound to a single chat,
/// so it is created per request instead of being registered in DI.
/// </summary>
public class ChatHistoryChatTool : IChatTool
{
    private const int DefaultLimit = 500;
    private const int MaxLimit = 1000;
    private const string DateFormat = "yyyy-MM-dd";
    // Supergroup and channel ids are -100XXXXXXXXXX; t.me/c links take the XXXXXXXXXX part
    private const long SupergroupIdOffset = -1_000_000_000_000;

    // Keep Cyrillic unescaped so the history doesn't blow up the token count
    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public string FunctionName => "search_chat_history";

    public ChatTool Definition { get; } = ChatTool.CreateFunctionTool(
        functionName: "search_chat_history",
        functionDescription: "Поиск по истории сообщений текущего чата. Все фильтры необязательны и комбинируются через И. " +
                             "Возвращает самые свежие подходящие сообщения в хронологическом порядке (время в UTC). " +
                             "Используй, когда спрашивают, кто что писал, о чём говорили, когда упоминали тему. " +
                             "Можешь обращаться к истории и по своей инициативе, чтобы общаться персональнее: вспомнить, " +
                             "что собеседник писал раньше, чем он интересуется, как обычно общается. " +
                             "Не ищи здесь текущую цепочку реплаев — она уже есть в сообщениях выше. " +
                             "author — кто писал, text — что написано. Если спрашивают о человеке («что думаешь о Насте», " +
                             "«что за тип Дима»), сначала ищи по author — так ты увидишь, что пишет он сам; " +
                             "поиск по text с его именем вернёт только то, что о нём говорят другие. " +
                             "Твои собственные прошлые ответы в выдачу не попадают. " +
                             "При выводах опирайся в первую очередь на недавние сообщения: они точнее отражают, " +
                             "что человек думает и чем живёт сейчас; старые — лишь фон. " +
                             "Если спрашивают, где или когда кто-то что-то писал («где это он такое говорил?», «скинь ссылку»), " +
                             "найди это сообщение и дай ссылку на него: к link_prefix из ответа допиши id сообщения. " +
                             "Ссылку бери только из результатов поиска, не выдумывай id.",
        functionParameters: BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                user_id = new
                {
                    type = "integer",
                    description = "Telegram ID автора сообщений"
                },
                author = new
                {
                    type = "string",
                    description = "Автор сообщений (кто писал): часть username, имени или фамилии, например «вася» или «@vasya». Регистр, пробелы, точки " +
                                  "и раскладка не важны: «нтг» найдёт «N. T. G.», «dima» найдёт «Дима». " +
                                  "Если подходят несколько участников, вместо сообщений вернётся список кандидатов — " +
                                  "тогда уточни у собеседника, кого он имел в виду, перечислив их, или повтори поиск с user_id нужного"
                },
                date_from = new
                {
                    type = "string",
                    description = "Начальная дата включительно в формате yyyy-MM-dd"
                },
                date_to = new
                {
                    type = "string",
                    description = "Конечная дата включительно в формате yyyy-MM-dd"
                },
                text = new
                {
                    type = "string",
                    description = "Что искать в тексте сообщений (о чём писали, не кто): ключевые слова в любой форме " +
                                  "(поиск понимает склонения: «машина» найдёт «машину»). " +
                                  "Поддерживает \"точную фразу\", or для альтернатив и -слово для исключения. " +
                                  "Если по словам ничего не нашлось, ищется подстрока — так находятся сленг и части слов"
                },
                limit = new
                {
                    type = "integer",
                    description = $"Максимум сообщений в ответе, по умолчанию {DefaultLimit}, не больше {MaxLimit}. " +
                                  "Для обобщённых вопросов о человеке или теме («что думаешь о нём», «как он обычно общается») " +
                                  "бери много; для точечных («что он писал вчера про работу») можно меньше"
                }
            }
        })
    );

    private readonly IMessageRepository _messageRepository;
    private readonly long _chatId;
    private readonly string? _linkPrefix;
    private readonly ILogger<ChatHistoryChatTool> _logger;

    public ChatHistoryChatTool(IMessageRepository messageRepository, long chatId, ILogger<ChatHistoryChatTool> logger)
    {
        _messageRepository = messageRepository;
        _chatId = chatId;
        _linkPrefix = GetLinkPrefix(chatId);
        _logger = logger;
    }

    public async Task<string> ExecuteAsync(string arguments, CancellationToken сancellationToken)
    {
        using var doc = JsonDocument.Parse(arguments);
        var root = doc.RootElement;

        long? userId = root.TryGetProperty("user_id", out var userIdElement) && userIdElement.TryGetInt64(out var parsedUserId)
            ? parsedUserId
            : null;
        var author = GetString(root, "author");
        var text = GetString(root, "text");

        DateTime? from = null;
        var dateFromString = GetString(root, "date_from");
        if (dateFromString != null)
        {
            if (!TryParseDate(dateFromString, out var dateFrom))
                return """{"error":"date_from должна быть в формате yyyy-MM-dd"}""";
            from = dateFrom;
        }

        DateTime? to = null;
        var dateToString = GetString(root, "date_to");
        if (dateToString != null)
        {
            if (!TryParseDate(dateToString, out var dateTo))
                return """{"error":"date_to должна быть в формате yyyy-MM-dd"}""";
            // Inclusive end date: take everything before the start of the next day
            to = dateTo.AddDays(1);
        }

        var limit = root.TryGetProperty("limit", out var limitElement) && limitElement.TryGetInt32(out var parsedLimit)
            ? Math.Clamp(parsedLimit, 1, MaxLimit)
            : DefaultLimit;

        if (userId == null && author != null)
        {
            var authors = ChatAuthorMatcher.Find(await _messageRepository.GetChatAuthorsAsync(_chatId, сancellationToken), author);
            var exactMatch = ChatAuthorMatcher.FindExactUsername(authors, author);

            _logger.LogInformation("Author \"{Author}\" (search key \"{Key}\") matched {Count} chat members: {Candidates}",
                author, ChatAuthorMatcher.ToSearchKey(author), authors.Count, authors.Count == 0 ? "none" : string.Join(", ", authors.Select(ChatAuthorMatcher.FormatCandidate)));

            if (exactMatch != null)
            {
                _logger.LogInformation("Author \"{Author}\" resolved by exact username to {Candidate}", author, ChatAuthorMatcher.FormatCandidate(exactMatch));
                userId = exactMatch.UserId;
            }
            else if (authors.Count == 0)
            {
                // Show who is in the chat, so the model asks the user instead of roasting someone else's messages
                return JsonSerializer.Serialize(new
                {
                    messages = Array.Empty<object>(),
                    note = $"В чате нет участника «{author}». Не подставляй сообщения других людей (и собеседника) вместо него: " +
                           "если кто-то из списка ниже явно тот, кого имели в виду, повтори поиск с его user_id, иначе спроси у собеседника",
                    chat_members = (await _messageRepository.GetChatAuthorsAsync(_chatId, сancellationToken))
                        .Take(ChatAuthorMatcher.MaxCandidates * 3)
                        .Select(a => new { user_id = a.UserId, username = a.Username, name = $"{a.FirstName} {a.LastName}".Trim() })
                }, JsonOptions);
            }
            else if (authors.Count > 1)
            {
                return JsonSerializer.Serialize(new
                {
                    ambiguous_author = true,
                    note = "Под автора подходят несколько участников. Спроси у собеседника, кого он имел в виду, перечислив варианты",
                    candidates = authors.Select(a => new
                    {
                        user_id = a.UserId,
                        username = a.Username,
                        name = $"{a.FirstName} {a.LastName}".Trim(),
                        messages_in_chat = a.MessageCount
                    })
                }, JsonOptions);
            }
            else
            {
                _logger.LogInformation("Author \"{Author}\" resolved to the only match {Candidate}", author, ChatAuthorMatcher.FormatCandidate(authors[0]));
                userId = authors[0].UserId;
            }
        }

        _logger.LogInformation("Searching chat {ChatId} history: user {UserId}, text \"{Text}\", from {From:yyyy-MM-dd}, to {To:yyyy-MM-dd}, limit {Limit}",
            _chatId, userId, text, from, to?.AddDays(-1), limit);

        var messages = await _messageRepository.SearchMessagesAsync(_chatId, userId, from, to, text, limit, сancellationToken);
        if (messages.Count == 0)
        {
            return """{"messages":[],"note":"Ничего не найдено"}""";
        }

        // Not the whole result (it can be hundreds of messages) — just enough to see who and what period the model got
        var authorStats = messages
            .GroupBy(m => FormatAuthor(m.User))
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key}: {g.Count()}");
        _logger.LogInformation("Chat history search returned {Count} messages from {First:yyyy-MM-dd HH:mm} to {Last:yyyy-MM-dd HH:mm}, by author: {Authors}",
            messages.Count, messages[0].MessageDate, messages[^1].MessageDate, string.Join(", ", authorStats));

        // One line per message instead of a JSON object: with hundreds of messages the field names alone cost thousands of tokens
        const string order = "от старых к новым: последние строки — самые свежие, им отдавай приоритет";
        if (_linkPrefix == null)
        {
            return JsonSerializer.Serialize(new
            {
                count = messages.Count,
                format = "дата время (UTC) | автор | текст",
                order,
                messages = messages.Select(m => $"{FormatDate(m)} | {FormatAuthor(m.User)} | {m.Text}")
            }, JsonOptions);
        }

        // Only the id per line: the full link on every one of hundreds of lines would cost far more tokens
        return JsonSerializer.Serialize(new
        {
            count = messages.Count,
            format = "id | дата время (UTC) | автор | текст",
            order,
            link_prefix = _linkPrefix,
            link_example = $"{_linkPrefix}{messages[^1].Id}",
            messages = messages.Select(m => $"{m.Id} | {FormatDate(m)} | {FormatAuthor(m.User)} | {m.Text}")
        }, JsonOptions);
    }

    /// <summary>
    /// Base of a link to a message in the chat, like https://t.me/c/2680016267/. Only supergroups have
    /// such links (they open for chat members), so for other chats there is nothing to link to.
    /// </summary>
    private static string? GetLinkPrefix(long chatId)
    {
        if (chatId > SupergroupIdOffset)
        {
            return null;
        }

        return $"https://t.me/c/{SupergroupIdOffset - chatId}/";
    }

    private static string FormatDate(MessageEntity message) =>
        message.MessageDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string FormatAuthor(UserEntity? user)
    {
        if (user == null)
            return "?";

        var name = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrEmpty(user.Username) ? name : $"{name} (@{user.Username})";
    }

    private static string? GetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.String)
            return null;
        var value = element.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool TryParseDate(string value, out DateTime date)
    {
        var parsed = DateOnly.TryParseExact(value.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly);
        date = parsed ? dateOnly.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : default;
        return parsed;
    }
}
