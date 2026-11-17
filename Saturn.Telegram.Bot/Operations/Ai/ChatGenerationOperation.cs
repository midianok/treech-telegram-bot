using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Saturn.Bot.Service.Extensions;
using Saturn.Bot.Service.Options;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Bot.Service.Services.Prompts;
using Saturn.Bot.Service.Services.Tools;
using Saturn.Bot.Service.Services.Tools.Utils;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Saturn.Telegram.Lib.Exceptions;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Operation;
using Saturn.Telegram.Lib.Attributes;
using Saturn.Telegram.Lib.Infrastructure.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

[GlobalCooldown(5)]
[ChatOnly("иди общайся в чат, хитрый пидарас")]
public class ChatGenerationOperation : IOperation
{
    private static readonly string[] DayNames = ["воскресенье", "понедельник", "вторник", "среда", "четверг", "пятница", "суббота"];

    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IAiService _aiService;
    private readonly ISaveMessageService _saveMessageService;
    private readonly IChatCachedRepository _chatCachedRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IReadOnlyList<IChatTool> _tools;
    private readonly ChatToolFactory _chatToolFactory;
    private readonly ILogger<ChatGenerationOperation> _logger;
    private readonly string _invokeCommand;
    private readonly string _botUsername;

    public ChatGenerationOperation(
        ITelegramBotClient telegramBotClient,
        IAiService aiService,
        ISaveMessageService saveMessageService,
        IChatCachedRepository chatCachedRepository,
        IMessageRepository messageRepository,
        IEnumerable<IChatTool> tools,
        ChatToolFactory chatToolFactory,
        IOptions<BotOptions> botOptions,
        ILogger<ChatGenerationOperation> logger)
    {
        _telegramBotClient = telegramBotClient;
        _aiService = aiService;
        _saveMessageService = saveMessageService;
        _chatCachedRepository = chatCachedRepository;
        _messageRepository = messageRepository;
        _tools = tools.ToList();
        _chatToolFactory = chatToolFactory;
        _logger = logger;
        _invokeCommand = botOptions.Value.InvokeCommand;
        _botUsername = botOptions.Value.BotUsername;
    }

    public bool Validate(Message msg, UpdateType type)
    {
        var text = msg.Text ?? msg.Caption;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return text.StartsWith($"{_invokeCommand} ", StringComparison.CurrentCultureIgnoreCase) ||
               text.StartsWith($"{_invokeCommand}, ", StringComparison.CurrentCultureIgnoreCase) ||
               IsReplyToBot(msg);
    }

    public async Task OnMessageAsync(Message msg, UpdateType type, CancellationToken сancellationToken)
    {
        var request = ExtractRequest(msg);
        var senderName = GetSenderName(msg.From);
        var chatEntity = await _chatCachedRepository.GetAsync(msg.Chat.Id);
        var images = await DownloadImagesAsync(msg, сancellationToken);

        var messages = new List<ChatMessage>();
        AddSystemPrompts(messages, chatEntity);
        await AddHistoryAsync(messages, msg);
        AddRequestNotes(messages, msg, senderName);
        AddRequest(messages, request, senderName, images);

        var toolContext = new ChatToolContext(msg, request, images);
        var tools = _tools.Concat(_chatToolFactory.Create(toolContext)).ToList();
        
        _logger.LogInformation("Chat generation context: agent {Agent}, sender {Sender}, images {ImageCount}",
            chatEntity.AiAgent?.Name ?? "none", senderName, images.Count);
        
        var aiResult = await _telegramBotClient.WithChatActionAsync(msg.Chat, ChatAction.Typing,
            () => _aiService.CompleteChatAsync(messages, tools, сancellationToken), сancellationToken);

        var result = StripMarkdown(aiResult.Text);
        if (string.IsNullOrWhiteSpace(result))
        {
            if (!toolContext.ActionTaken)
            {
                throw new AiEmptyResponseException();
            }

            _logger.LogInformation("Empty text answer after a chat action, sending nothing");
            return;
        }

        var reply = await _telegramBotClient.SendMessage(msg.Chat, result, ParseMode.None,
            new ReplyParameters { MessageId = msg.Id }, cancellationToken: сancellationToken);
        await _saveMessageService.SaveMessageAsync(reply, aiResult.ToolCalls);
    }

    private string ExtractRequest(Message msg) =>
        (msg.Text ?? msg.Caption)!.ToLower()
        .Replace($"{_invokeCommand}, ", string.Empty)
        .Replace($"{_invokeCommand} ", string.Empty);

    private static void AddSystemPrompts(List<ChatMessage> messages, ChatEntity chatEntity)
    {
        if (!string.IsNullOrEmpty(chatEntity.AiAgent?.Prompt))
        {
            messages.Add(new SystemChatMessage(chatEntity.AiAgent.Prompt));
        }

        messages.Add(new SystemChatMessage(PromptLibrary.Get("Chat/Behavior")));
    }

    private async Task AddHistoryAsync(List<ChatMessage> messages, Message msg)
    {
        if (IsReplyToBot(msg))
        {
            await AddBotReplyChainAsync(messages, msg);
            return;
        }

        var repliedText = msg.ReplyToMessage?.Text ?? msg.ReplyToMessage?.Caption;
        if (!string.IsNullOrWhiteSpace(repliedText))
        {
            messages.Add(new UserChatMessage(WithSender(GetSenderName(msg.ReplyToMessage!.From), repliedText)));
        }
    }

    private async Task AddBotReplyChainAsync(List<ChatMessage> messages, Message msg)
    {
        var messageChain = await _messageRepository.GetMessageChainAsync(msg.Chat.Id, msg.ReplyToMessage!.Id);
        if (messageChain.Count == 0)
        {
            _logger.LogInformation("Reply to bot: message {MessageId} not found in DB, using only the replied text",
                msg.ReplyToMessage.Id);
            messages.Add(new UserChatMessage(msg.ReplyToMessage.Text));
            return;
        }

        foreach (var chainMessage in messageChain.OrderBy(x => x.MessageDate))
        {
            if (chainMessage.IsBot)
            {
                messages.Add(new AssistantChatMessage(chainMessage.Text ?? string.Empty));
                var toolCallsNote = GetToolCallsNote(chainMessage.AiToolCalls);
                if (toolCallsNote != null)
                {
                    messages.Add(new SystemChatMessage(toolCallsNote));
                }

                continue;
            }

            messages.Add(new UserChatMessage(WithSender(GetSenderName(chainMessage.User), chainMessage.Text)));
        }

        _logger.LogInformation("Reply to bot: loaded chain of {Count} messages", messageChain.Count);
    }

    private static void AddRequestNotes(List<ChatMessage> messages, Message msg, string senderName)
    {
        messages.Add(new SystemChatMessage(GetCurrentDateTimeNote()));
        if (!ChatToolFactory.CanModerate(msg))
        {
            messages.Add(new SystemChatMessage(PromptLibrary.Get("Chat/NoModeration")));
        }

        if (!string.IsNullOrEmpty(senderName))
        {
            messages.Add(new SystemChatMessage(PromptLibrary.Get("Chat/CurrentSender", new { sender = senderName })));
        }
    }

    private static void AddRequest(List<ChatMessage> messages, string request, string senderName, IReadOnlyList<byte[]> images)
    {
        var requestText = WithSender(senderName, request);
        if (images.Count == 0)
        {
            messages.Add(new UserChatMessage(requestText));
            return;
        }

        messages.Add(new SystemChatMessage(PromptLibrary.Get("Chat/ImageAttached")));
        messages.Add(new UserChatMessage(images
            .Select(image =>
                ChatMessageContentPart.CreateImagePart(new BinaryData(image), "image/jpeg", ChatImageDetailLevel.Auto))
            .Append(ChatMessageContentPart.CreateTextPart(requestText))));
    }

    private string StripMarkdown(string rawResult)
    {
        var result = rawResult.StripMarkdown();
        if (result != rawResult)
        {
            _logger.LogInformation("Markdown stripped from the answer: {Before} -> {After} chars", rawResult.Length,
                result.Length);
        }

        return result;
    }

    [return: NotNullIfNotNull(nameof(text))]
    private static string? WithSender(string senderName, string? text) => 
        string.IsNullOrEmpty(senderName) ? text : $"[{senderName}]: {text}";

    private async Task<IReadOnlyList<byte[]>> DownloadImagesAsync(Message msg, CancellationToken сancellationToken)
    {
        var images = new List<byte[]>();
        foreach (var photo in new[] { msg.ReplyToMessage?.Photo, msg.Photo })
        {
            var largest = photo?.MaxBy(x => x.FileSize);
            if (largest != null)
            {
                images.Add(await _telegramBotClient.DownloadFileAsync(largest.FileId, сancellationToken));
            }
        }

        return images;
    }
    
    private static string GetCurrentDateTimeNote()
    {
        ReminderFormatting.TryGetTimeZone(ReminderFormatting.DefaultTimeZone, out var timeZone);
        var utcNow = DateTime.UtcNow;
        var now = TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);
        var offset = timeZone.GetUtcOffset(utcNow);
        return PromptLibrary.Get("Chat/CurrentDateTime", new
        {
            date = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            dayOfWeek = DayNames[(int)now.DayOfWeek],
            time = now.ToString("HH:mm", CultureInfo.InvariantCulture),
            timeZone = timeZone.Id,
            utcOffset = $"{(offset < TimeSpan.Zero ? "-" : "+")}{offset:hh\\:mm}"
        });
    }
    
    private static string? GetToolCallsNote(string? aiToolCalls)
    {
        if (aiToolCalls == null)
        {
            return null;
        }

        return aiToolCalls.Length == 0
            ? PromptLibrary.Get("Chat/NoToolsCalled")
            : PromptLibrary.Get("Chat/ToolsCalled", new { tools = aiToolCalls.Replace(",", ", ") });
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
        if (string.IsNullOrEmpty(_botUsername) || msg.ReplyToMessage == null || msg.ReplyToMessage.From == null)
        {
            return false;
        }

        return msg.ReplyToMessage.Type == MessageType.Text && msg.ReplyToMessage.From.Username == _botUsername;
    }
}