using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
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
    private const int MaxAuthorCandidates = 10;
    private const string DateFormat = "yyyy-MM-dd";

    private static readonly Dictionary<char, string> CyrillicToLatin = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "e", ['ж'] = "zh",
        ['з'] = "z", ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o",
        ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u", ['ф'] = "f", ['х'] = "h", ['ц'] = "ts",
        ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sch", ['ъ'] = "", ['ы'] = "y", ['ь'] = "", ['э'] = "e", ['ю'] = "yu",
        ['я'] = "ya"
    };

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
                             "что человек думает и чем живёт сейчас; старые — лишь фон.",
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

    public ChatHistoryChatTool(IMessageRepository messageRepository, long chatId)
    {
        _messageRepository = messageRepository;
        _chatId = chatId;
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
            var authors = FindAuthors(await _messageRepository.GetChatAuthorsAsync(_chatId, сancellationToken), author);
            var normalizedAuthor = author.Trim().TrimStart('@');
            var exactMatch = authors.FirstOrDefault(a => string.Equals(a.Username, normalizedAuthor, StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null)
            {
                userId = exactMatch.UserId;
            }
            else if (authors.Count == 0)
            {
                return JsonSerializer.Serialize(new { messages = Array.Empty<object>(), note = $"В чате нет участника «{author}»" }, JsonOptions);
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
                userId = authors[0].UserId;
            }
        }

        var messages = await _messageRepository.SearchMessagesAsync(_chatId, userId, from, to, text, limit, сancellationToken);
        if (messages.Count == 0)
        {
            return """{"messages":[],"note":"Ничего не найдено"}""";
        }

        // One line per message instead of a JSON object: with hundreds of messages the field names alone cost thousands of tokens
        return JsonSerializer.Serialize(new
        {
            count = messages.Count,
            format = "дата время (UTC) | автор | текст",
            order = "от старых к новым: последние строки — самые свежие, им отдавай приоритет",
            messages = messages.Select(m =>
                $"{m.MessageDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} | {FormatAuthor(m.User)} | {m.Text}")
        }, JsonOptions);
    }

    private static List<ChatAuthor> FindAuthors(List<ChatAuthor> chatAuthors, string author)
    {
        var key = ToSearchKey(author);
        if (key.Length == 0)
            return [];

        return chatAuthors
            .Where(a => new[] { a.Username, a.FirstName, a.LastName, $"{a.FirstName} {a.LastName}" }
                .Any(name => ToSearchKey(name).Contains(key)))
            .Take(MaxAuthorCandidates)
            .ToList();
    }

    /// <summary>
    /// Brings a name to a comparable form: lowercase latin letters and digits only,
    /// so "N. T. G." and "нтг" both become "ntg".
    /// </summary>
    private static string ToSearchKey(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        foreach (var c in value.ToLowerInvariant())
        {
            if (CyrillicToLatin.TryGetValue(c, out var latin))
                builder.Append(latin);
            else if (char.IsLetterOrDigit(c))
                builder.Append(c);
        }
        return builder.ToString();
    }

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
