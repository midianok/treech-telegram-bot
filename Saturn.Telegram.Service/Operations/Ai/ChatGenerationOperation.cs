using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Saturn.Telegram.Lib.Operation;
using Saturn.Telegram.Lib.Services.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

public class ChatGenerationOperation : OperationBase
{
    private readonly ChatClient _chatClient;
    private readonly ISaveMessageService _saveMessageService;
    private readonly IChatCachedRepository _chatCachedRepository;

    public ChatGenerationOperation(ChatClient chatClient, ISaveMessageService saveMessageService, IChatCachedRepository chatCachedRepository)
    {
        _chatClient = chatClient;
        _saveMessageService = saveMessageService;
        _chatCachedRepository = chatCachedRepository;
    }
    
    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var request = msg.Text!.ToLower()
            .Replace("трич, ", string.Empty)
            .Replace("трич ", string.Empty);
        
        var messages = new List<ChatMessage>();
        
        var chatEntity = await _chatCachedRepository.GetAsync(msg.Chat.Id);
        if (!string.IsNullOrEmpty(chatEntity.AiAgent?.Prompt))
        {
            messages.Add(new SystemChatMessage(chatEntity.AiAgent.Prompt));
        }
        messages.Add(new UserChatMessage(request));
        
        await TelegramBotClient.SendChatAction(msg.Chat.Id, ChatAction.Typing);

        try
        {
            var clientResult = await _chatClient.CompleteChatAsync(messages);
            var result = clientResult.Value.Content.FirstOrDefault()?.Text;
            
            var reply = await TelegramBotClient.SendMessage(msg.Chat, result ?? "что-то пошло не так", ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id });
            await _saveMessageService.SaveMessageAsync(reply);
        }
        catch (Exception e)
        {
            await TelegramBotClient.SendMessage(msg.Chat, "что-то пошло не так", ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id });
            Logger.LogError(e, e.Message);
        }
    }

    protected override bool ValidateOnTextMessage(Message msg, UpdateType type)
    {
        var isReply = msg.ReplyToMessage is { Type: MessageType.Text, From.Username: "ilya_dev_bot" };

        return msg.Text!.StartsWith("трич ", StringComparison.CurrentCultureIgnoreCase) ||
               msg.Text!.StartsWith("трич, ", StringComparison.CurrentCultureIgnoreCase) || isReply;
    }
}