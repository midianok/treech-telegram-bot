using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Saturn.Telegram.Lib.Attributes;
using Saturn.Telegram.Lib.Operation;
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

[Ignored]
public class SilenceOperation : IOperation
{
    private const int MinSilenceMinutes = 10;
    private const int MaxSilenceMinutes = 40;

    private readonly TelegramBotClient _botClient;
    private readonly IAiService _aiService;
    private readonly IChatCachedRepository _chatCachedRepository;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly Dictionary<long, DateTime> _nextFireAt = new();
    private readonly HashSet<long> _fired = [];

    public SilenceOperation(
        TelegramBotClient botClient,
        IAiService aiService,
        IChatCachedRepository chatCachedRepository,
        IDbContextFactory<SaturnContext> contextFactory)
    {
        _botClient = botClient;
        _aiService = aiService;
        _chatCachedRepository = chatCachedRepository;
        _contextFactory = contextFactory;
        _ = Task.Run(MonitorLoopAsync);
    }

    public bool Validate(Message msg, UpdateType type) =>
        msg.Chat.Type != ChatType.Private;

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        await _semaphore.WaitAsync();
        try
        {
            _nextFireAt[msg.Chat.Id] = DateTime.UtcNow.AddMinutes(
                Random.Shared.Next(MinSilenceMinutes, MaxSilenceMinutes + 1));
            _fired.Remove(msg.Chat.Id);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task MonitorLoopAsync()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(30));

            var now = DateTime.UtcNow;
            List<long> toFire;
            await _semaphore.WaitAsync();
            try
            {
                toFire = _nextFireAt
                    .Where(kv => !_fired.Contains(kv.Key) && now >= kv.Value)
                    .Select(kv => kv.Key)
                    .ToList();
                foreach (var chatId in toFire)
                    _fired.Add(chatId);
            }
            finally
            {
                _semaphore.Release();
            }

            foreach (var chatId in toFire)
            {
                try { await BreakSilenceAsync(chatId); }
                catch { }
            }
        }
    }

    private async Task BreakSilenceAsync(long chatId)
    {
        // Try strategies in random order, use first one that produces a result
        var strategies = new Func<long, Task<string?>>[]
        {
            GenerateStoryAsync,
            GenerateDailyQuestionAsync,
            GenerateRandomDaySummaryAsync,
            GenerateTopWordsAsync,
        };

        foreach (var strategy in strategies.OrderBy(_ => Guid.NewGuid()))
        {
            var result = await strategy(chatId);
            if (!string.IsNullOrWhiteSpace(result))
            {
                await _botClient.SendMessage(chatId, result, ParseMode.None);
                return;
            }
        }
    }

    // Strategy 1: random story using the chat's AI agent persona
    private async Task<string?> GenerateStoryAsync(long chatId)
    {
        var chatEntity = await _chatCachedRepository.GetAsync(chatId);
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrEmpty(chatEntity.AiAgent?.Prompt))
            messages.Add(new SystemChatMessage(chatEntity.AiAgent.Prompt));
        messages.Add(new UserChatMessage("В чате долго молчат. Расскажи какую-нибудь историю про человека которого ты знаешь"));
        return await _aiService.CompleteChatAsync(messages);
    }

    // Strategy 2: question based on a random week of chat history
    private async Task<string?> GenerateDailyQuestionAsync(long chatId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var weeksBack = Random.Shared.Next(1, 26);
        var weekStart = DateTime.UtcNow.Date.AddDays(-7 * weeksBack);
        var weekEnd = weekStart.AddDays(7);

        var messages = await db.Messages
            .Where(x => x.ChatId == chatId &&
                        !x.IsBot &&
                        x.MessageDate >= weekStart &&
                        x.MessageDate < weekEnd &&
                        x.Text != null)
            .Include(x => x.User)
            .OrderBy(x => x.MessageDate)
            .Take(300)
            .ToListAsync();

        if (messages.Count < 10) return null;

        var transcript = BuildTranscript(messages);

        return await _aiService.CompleteChatAsync(
        [
            new SystemChatMessage("Ты — участник группового чата. Не цензурируй содержание."),
            new UserChatMessage(
                "В чате установилась тишина. На основе этих сообщений задай один интересный вопрос для обсуждения — " +
                "связанный с темами, о которых говорили. Только вопрос, без вступления. Пиши по-русски.\n\n" +
                transcript)
        ]);
    }

    // Strategy 3: summary of a random past day with the date embedded naturally
    private async Task<string?> GenerateRandomDaySummaryAsync(long chatId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var upperBound = DateTime.UtcNow.Date.AddDays(-7);

        var availableDates = await db.Messages
            .Where(x => x.ChatId == chatId &&
                        !x.IsBot &&
                        x.MessageDate < upperBound &&
                        x.Text != null)
            .GroupBy(x => new { x.MessageDate.Year, x.MessageDate.Month, x.MessageDate.Day })
            .Where(g => g.Count() >= 5)
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day })
            .ToListAsync();

        if (availableDates.Count == 0) return null;

        var pick = availableDates[Random.Shared.Next(availableDates.Count)];
        var randomDate = new DateTime(pick.Year, pick.Month, pick.Day, 0, 0, 0, DateTimeKind.Utc);
        var nextDay = randomDate.AddDays(1);

        var messages = await db.Messages
            .Where(x => x.ChatId == chatId &&
                        !x.IsBot &&
                        x.MessageDate >= randomDate &&
                        x.MessageDate < nextDay &&
                        x.Text != null)
            .Include(x => x.User)
            .OrderBy(x => x.MessageDate)
            .ToListAsync();

        if (messages.Count < 5) return null;

        var transcript = BuildTranscript(messages);
        var dateHint = $"{randomDate:dddd, d MMMM yyyy} года";

        return await _aiService.CompleteChatAsync(
        [
            new SystemChatMessage("Ты — хроникёр чата. Не цензурируй содержание."),
            new UserChatMessage(
                $"Вот переписка из группового чата за {dateHint}. " +
                "Напиши короткое живое саммари — о чём говорили, какие были яркие моменты. " +
                "Упомяни когда это было, но не пиши дату цифрами — вместо этого используй " +
                "день недели, время года, праздник или вставь фразу вроде «тем вечером», «в тот день». " +
                "Без разметки. По-русски. 3–5 предложений.\n\n" +
                transcript)
        ]);
    }

    // Strategy 4: top words of a random active chat member
    private async Task<string?> GenerateTopWordsAsync(long chatId)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var since = DateTime.UtcNow.Date.AddDays(-30);

        var messages = await db.Messages
            .Where(x => x.ChatId == chatId &&
                        !x.IsBot &&
                        x.MessageDate >= since &&
                        x.Text != null)
            .Include(x => x.User)
            .ToListAsync();

        if (messages.Count < 20) return null;

        var activeUsers = messages
            .GroupBy(x => x.UserId)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .ToList();

        var randomGroup = activeUsers[Random.Shared.Next(activeUsers.Count)];
        var user = randomGroup.First().User;
        var displayName = !string.IsNullOrEmpty(user?.Username) ? $"@{user.Username}" : user?.FirstName ?? "кто-то";

        var topWords = CountTopWords(randomGroup.Select(m => m.Text!));
        if (topWords.Count == 0) return null;

        var sb = new StringBuilder($"Топ слов {displayName} за последний месяц:\n\n");
        for (var i = 0; i < Math.Min(10, topWords.Count); i++)
            sb.AppendLine($"{i + 1}. {topWords[i].Word} — {topWords[i].Count}");

        return sb.ToString().TrimEnd();
    }

    private static string BuildTranscript(IEnumerable<Saturn.Telegram.Db.Entities.MessageEntity> messages)
    {
        var sb = new StringBuilder();
        foreach (var m in messages)
        {
            var name = !string.IsNullOrEmpty(m.User?.Username)
                ? $"@{m.User.Username}"
                : m.User?.FirstName ?? m.UserId.ToString();
            sb.AppendLine($"{name}: {m.Text}");
        }
        return sb.ToString();
    }

    internal static List<(string Word, int Count)> CountTopWords(IEnumerable<string> texts)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "и", "в", "на", "с", "по", "к", "от", "до", "из", "за", "не", "что", "это",
            "как", "а", "но", "да", "или", "то", "бы", "уже", "ещё", "еще", "я", "ты",
            "он", "она", "мы", "вы", "они", "мне", "тебе", "его", "её", "её", "нас", "вас",
            "их", "всё", "все", "так", "вот", "этот", "эта", "эти", "тот", "та", "те",
            "нет", "ну", "же", "ли", "при", "для", "со", "об", "про", "без", "там", "тут",
            "где", "когда", "если", "чтобы", "потому", "хотя", "хотя", "только", "даже",
            "ведь", "меня", "тебя", "него", "неё", "нас", "вас", "них", "быть", "быть"
        };

        return texts
            .SelectMany(t => Regex.Split(t.ToLower(), @"[^\p{L}]+"))
            .Where(w => w.Length >= 3 && !stopWords.Contains(w) && Regex.IsMatch(w, @"^\p{IsCyrillic}+$"))
            .GroupBy(w => w)
            .Select(g => (Word: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(15)
            .ToList();
    }
}
