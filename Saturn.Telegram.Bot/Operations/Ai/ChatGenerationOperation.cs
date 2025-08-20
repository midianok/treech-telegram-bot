using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Extensions;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
// ReSharper disable MethodSupportsCancellation

namespace Saturn.Bot.Service.Operations.Ai;

public class ChatGenerationOperation : OperationBase
{
    private readonly ChatClient _chatClient;
    private readonly ISaveMessageService _saveMessageService;
    private readonly IChatCachedRepository _chatCachedRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IMemoryCache _memoryCache;
    private readonly string _invokeCommand;
    
    public ChatGenerationOperation(ChatClient chatClient, ISaveMessageService saveMessageService, IChatCachedRepository chatCachedRepository, IMessageRepository messageRepository, IMemoryCache memoryCache, IConfiguration configuration)
    {
        _chatClient = chatClient;
        _saveMessageService = saveMessageService;
        _chatCachedRepository = chatCachedRepository;
        _memoryCache = memoryCache;
        _messageRepository = messageRepository;
        _invokeCommand = configuration.GetSectionOrThrow("INVOKE_COMMAND");
    }
    
    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
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

        await TelegramBotClient.SendChatAction(msg.Chat, ChatAction.Typing);
        var clientResult = await _chatClient.CompleteChatAsync(messages);
        var result = clientResult.Value.Content.FirstOrDefault()?.Text;
            
        var reply = await TelegramBotClient.SendMessage(msg.Chat, result ?? "что-то пошло не так", ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id });
        await _saveMessageService.SaveMessageAsync(reply);
    }

    protected override bool ValidateMessage(Message msg, UpdateType type)
    {
        if (string.IsNullOrEmpty(msg.Text))
        {
            return false;
        }
        var isReplyToBot = IsReplyToBot(msg);
        return msg.Text!.StartsWith($"{_invokeCommand} ", StringComparison.CurrentCultureIgnoreCase) ||
               msg.Text!.StartsWith($"{_invokeCommand}, ", StringComparison.CurrentCultureIgnoreCase) ||
               isReplyToBot;
    }

    private bool IsReplyToBot(Message msg)
    {
        var bot = _memoryCache.GetOrCreate($"{nameof(ChatGenerationOperation)}_user_bot", async _ => await TelegramBotClient.GetMe())?.GetAwaiter().GetResult();
        if (bot == null ||  msg.ReplyToMessage ==null || msg.ReplyToMessage.From == null)
        {
            return false;
        }
        return  msg.ReplyToMessage.Type == MessageType.Text && msg.ReplyToMessage.From.Username == bot.Username;
    }
}