using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

public class SilenceOperation : IOperation
{
    private const string Prompt = "В чате долго молчат. Расскажи какойю-нибудь историю или тегни Username человека которого ты знаешь и скажи ему что-нибудь";

    private const int MinSilenceMinutes = 10;
    private const int MaxSilenceMinutes = 40;

    private readonly TelegramBotClient _botClient;
    private readonly IAiService _aiService;
    private readonly IChatCachedRepository _chatCachedRepository;

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly Dictionary<long, DateTime> _nextFireAt = new();
    private readonly HashSet<long> _fired = [];

    public SilenceOperation(TelegramBotClient botClient, IAiService aiService, IChatCachedRepository chatCachedRepository)
    {
        _botClient = botClient;
        _aiService = aiService;
        _chatCachedRepository = chatCachedRepository;
        _ = Task.Run(MonitorLoopAsync);
    }

    public bool Validate(Message msg, UpdateType type) =>
        msg.Chat.Type != ChatType.Private;

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        await _semaphore.WaitAsync();
        try
        {
            _nextFireAt[msg.Chat.Id] = DateTime.UtcNow.AddMinutes(Random.Shared.Next(MinSilenceMinutes, MaxSilenceMinutes + 1));
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
                {
                    _fired.Add(chatId);
                }
            }
            finally
            {
                _semaphore.Release();
            }

            foreach (var chatId in toFire)
            {
                try
                {
                    var chatEntity = await _chatCachedRepository.GetAsync(chatId);
                    var messages = new List<ChatMessage>();
                    if (!string.IsNullOrEmpty(chatEntity.AiAgent?.Prompt))
                    {
                        messages.Add(new SystemChatMessage(chatEntity.AiAgent.Prompt));
                    }
                    messages.Add(new UserChatMessage(Prompt));
                    var result = await _aiService.CompleteChatAsync(messages);
                    await _botClient.SendMessage(chatId, result, ParseMode.Markdown);
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}
