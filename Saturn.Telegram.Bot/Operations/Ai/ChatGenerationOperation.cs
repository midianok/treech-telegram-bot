using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Extensions;
using Saturn.Telegram.Lib;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Saturn.Telegram.Lib.Operation;
using System.ClientModel;
using Saturn.Telegram.Lib.Attributes;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
// ReSharper disable MethodSupportsCancellation

namespace Saturn.Bot.Service.Operations.Ai;

[GlobalCooldown(10)]
public class ChatGenerationOperation : IOperation
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly ChatClient _chatClient;
    private readonly ISaveMessageService _saveMessageService;
    private readonly IChatCachedRepository _chatCachedRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<ChatGenerationOperation> _logger;
    private readonly string _invokeCommand;

    public ChatGenerationOperation(
        TelegramBotClient telegramBotClient,
        ChatClient chatClient,
        ISaveMessageService saveMessageService,
        IChatCachedRepository chatCachedRepository,
        IMessageRepository messageRepository,
        IMemoryCache memoryCache,
        IConfiguration configuration,
        ILogger<ChatGenerationOperation> logger)
    {
        _telegramBotClient = telegramBotClient;
        _chatClient = chatClient;
        _saveMessageService = saveMessageService;
        _chatCachedRepository = chatCachedRepository;
        _memoryCache = memoryCache;
        _messageRepository = messageRepository;
        _logger = logger;
        _invokeCommand = configuration.GetSectionOrThrow("INVOKE_COMMAND");
    }

    public bool Validate(Message msg, UpdateType type)
    {
        if (string.IsNullOrEmpty(msg.Text))
        {
            return false;
        }

        return msg.Text.StartsWith($"{_invokeCommand} ", StringComparison.CurrentCultureIgnoreCase) ||
               msg.Text.StartsWith($"{_invokeCommand}, ", StringComparison.CurrentCultureIgnoreCase) ||
               IsReplyToBot(msg);
    }

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        if (msg.Chat.Type is not (ChatType.Group or ChatType.Supergroup))
        {
            await _telegramBotClient.SendMessage(msg.Chat, "иди общайся в чат, хитрый пидарас");
            return;
        }

        var request = msg.Text!.ToLower()
            .Replace($"{_invokeCommand}, ", string.Empty)
            .Replace($"{_invokeCommand} ", string.Empty);

        var messages = new List<ChatMessage>();

        var chatEntity = await _chatCachedRepository.GetAsync(msg.Chat.Id);
        if (!string.IsNullOrEmpty(chatEntity.AiAgent?.Prompt))
        {
            messages.Add(new SystemChatMessage(chatEntity.AiAgent.Prompt));
        }

        var isReplyToBot = IsReplyToBot(msg);
        if (isReplyToBot)
        {
            var messageChain = await _messageRepository.GetMessageChainAsync(msg.Chat.Id, msg.ReplyToMessage!.Id);
            if (messageChain.Count > 0)
            {
                var userChatMessages = messageChain.OrderBy(x => x.MessageDate)
                    .Select(x => new UserChatMessage(x.Text));
                messages.AddRange(userChatMessages);
            }
            else
            {
                messages.Add(new UserChatMessage(msg.ReplyToMessage.Text));
            }
        }

        if (!isReplyToBot && msg.ReplyToMessage is { Type: MessageType.Text } && !string.IsNullOrWhiteSpace(msg.ReplyToMessage.Text))
        {
            messages.Add(new UserChatMessage(msg.ReplyToMessage.Text));
        }

        messages.Add(new UserChatMessage(request));

        await _telegramBotClient.SendChatAction(msg.Chat, ChatAction.Typing);
        try
        {
            var clientResult = await _chatClient.CompleteChatAsync(messages);
            var result = clientResult.Value.Content.FirstOrDefault()?.Text;

            var reply = await _telegramBotClient.SendMessage(msg.Chat, result ?? "что-то пошло не так", ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id });
            await _saveMessageService.SaveMessageAsync(reply);
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            _logger.LogError("xAI balance exhausted (429 Too Many Requests)");
            await _telegramBotClient.SendMessage(msg.Chat, "денег нет, но вы держитесь", replyParameters: new ReplyParameters { MessageId = msg.Id });
        }
    }

    private bool IsReplyToBot(Message msg)
    {
        var bot = _memoryCache.GetOrCreate($"{nameof(ChatGenerationOperation)}_user_bot", async _ => await _telegramBotClient.GetMe())?.GetAwaiter().GetResult();
        if (bot == null || msg.ReplyToMessage == null || msg.ReplyToMessage.From == null)
        {
            return false;
        }
        return msg.ReplyToMessage.Type == MessageType.Text && msg.ReplyToMessage.From.Username == bot.Username;
    }
}
