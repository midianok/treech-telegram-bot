using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using Saturn.Bot.Service.Extensions;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Saturn.Telegram.Lib.Operation;
using Saturn.Telegram.Lib.Attributes;
using Saturn.Telegram.Lib.Infrastructure.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
// ReSharper disable MethodSupportsCancellation

namespace Saturn.Bot.Service.Operations.Ai;

[GlobalCooldown(10)]
[ChatOnly("иди общайся в чат, хитрый пидарас")]
public class ChatGenerationOperation : IOperation
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly IAiService _aiService;
    private readonly ISaveMessageService _saveMessageService;
    private readonly IChatCachedRepository _chatCachedRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly string _invokeCommand;

    public ChatGenerationOperation(
        TelegramBotClient telegramBotClient,
        IAiService aiService,
        ISaveMessageService saveMessageService,
        IChatCachedRepository chatCachedRepository,
        IMessageRepository messageRepository,
        IMemoryCache memoryCache,
        IConfiguration configuration)
    {
        _telegramBotClient = telegramBotClient;
        _aiService = aiService;
        _saveMessageService = saveMessageService;
        _chatCachedRepository = chatCachedRepository;
        _memoryCache = memoryCache;
        _messageRepository = messageRepository;
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
                    .Select(x =>
                    {
                        var senderName = GetSenderName(x.User);
                        var text = string.IsNullOrEmpty(senderName) ? x.Text : $"[{senderName}]: {x.Text}";
                        return new UserChatMessage(text);
                    });
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

        var senderName = GetSenderName(msg.From);
        if (!string.IsNullOrEmpty(senderName))
        {
            messages.Add(new SystemChatMessage($"Тебе сейчас пишет: {senderName}"));
        }

        messages.Add(new UserChatMessage(request));

        await _telegramBotClient.SendChatAction(msg.Chat, ChatAction.Typing);
        var result = await _aiService.CompleteChatAsync(messages);

        var reply = await _telegramBotClient.SendMessage(msg.Chat, result, ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id });
        await _saveMessageService.SaveMessageAsync(reply);
    }

    private static string GetSenderName(User? user)
    {
        if (user == null)
        {
            return string.Empty;
        }

        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrEmpty(user.Username) ? fullName : $"{fullName} (@{user.Username})";
    }

    private static string GetSenderName(UserEntity? user)
    {
        if (user == null)
        {
            return string.Empty;
        }

        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrEmpty(user.Username) ? fullName : $"{fullName} (@{user.Username})";
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
