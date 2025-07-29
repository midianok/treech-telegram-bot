using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Saturn.Telegram.Lib.Operation;
using Saturn.Telegram.Lib.Services.Abstractions;
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
    

    public ChatGenerationOperation(ChatClient chatClient, ISaveMessageService saveMessageService, IChatCachedRepository chatCachedRepository, IMessageRepository messageRepository, IMemoryCache memoryCache)
    {
        _chatClient = chatClient;
        _saveMessageService = saveMessageService;
        _chatCachedRepository = chatCachedRepository;
        _memoryCache = memoryCache;
        _messageRepository = messageRepository;
    }
    
    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var request = msg.Text!.ToLower()
            .Replace("трич, ", string.Empty)
            .Replace("трич ", string.Empty);
        
        using var typingCancellationTokenSource = new CancellationTokenSource();
        _ = SendTypingAsync(msg.Chat.Id, typingCancellationTokenSource.Token);
        
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
            var res = messageChain.OrderBy(x => x.MessageDate)
                .Select(x => new UserChatMessage(x.Text));
            messages.AddRange(res);
        }

        if (!isReplyToBot && msg.ReplyToMessage is { Type: MessageType.Text } && !string.IsNullOrWhiteSpace(msg.ReplyToMessage.Text))
        {
            messages.Add(new UserChatMessage(msg.ReplyToMessage.Text));
        }
        
        messages.Add(new UserChatMessage(request));

        try
        {
            var clientResult = await _chatClient.CompleteChatAsync(messages);
            var result = clientResult.Value.Content.FirstOrDefault()?.Text;

            await typingCancellationTokenSource.CancelAsync();
            var reply = await TelegramBotClient.SendMessage(msg.Chat, result ?? "что-то пошло не так", ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id });
            await _saveMessageService.SaveMessageAsync(reply);
        }
        catch (Exception e)
        {
            await TelegramBotClient.SendMessage(msg.Chat, "что-то пошло не так", ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id });
            await typingCancellationTokenSource.CancelAsync();
            Logger.LogError(e, e.Message);
        }
    }

    protected override bool ValidateMessage(Message msg, UpdateType type)
    {
        if (string.IsNullOrEmpty(msg.Text))
        {
            return false;
        }
        var isReplyToBot = IsReplyToBot(msg);
        return msg.Text!.StartsWith("трич ", StringComparison.CurrentCultureIgnoreCase) ||
               msg.Text!.StartsWith("трич, ", StringComparison.CurrentCultureIgnoreCase) ||
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

    private async Task SendTypingAsync(long chatId, CancellationToken cancellationToken)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        using var combinedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellationTokenSource.Token);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await TelegramBotClient.SendChatAction(chatId, ChatAction.Typing,
                    cancellationToken: combinedCancellationTokenSource.Token);
                await Task.Delay(TimeSpan.FromSeconds(5), combinedCancellationTokenSource.Token);
            }
        }
        catch (Exception)
        {
            // ignored
        }
    }
}