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
        var request = (msg.Text ?? msg.Caption)!.ToLower()
            .Replace($"{_invokeCommand}, ", string.Empty)
            .Replace($"{_invokeCommand} ", string.Empty);

        var messages = new List<ChatMessage>();

        var chatEntity = await _chatCachedRepository.GetAsync(msg.Chat.Id);
        if (!string.IsNullOrEmpty(chatEntity.AiAgent?.Prompt))
        {
            messages.Add(new SystemChatMessage(chatEntity.AiAgent.Prompt));
        }

        messages.Add(new SystemChatMessage(PromptLibrary.Get("Chat/Behavior")));

        var isReplyToBot = IsReplyToBot(msg);
        if (isReplyToBot)
        {
            var messageChain = await _messageRepository.GetMessageChainAsync(msg.Chat.Id, msg.ReplyToMessage!.Id);
            if (messageChain.Count > 0)
            {
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

                    var chainSenderName = GetSenderName(chainMessage.User);
                    var text = string.IsNullOrEmpty(chainSenderName)
                        ? chainMessage.Text
                        : $"[{chainSenderName}]: {chainMessage.Text}";
                    messages.Add(new UserChatMessage(text));
                }

                _logger.LogInformation("Reply to bot: loaded chain of {Count} messages", messageChain.Count);
            }
            else
            {
                _logger.LogInformation("Reply to bot: message {MessageId} not found in DB, using only the replied text",
                    msg.ReplyToMessage.Id);
                messages.Add(new UserChatMessage(msg.ReplyToMessage.Text));
            }
        }

        var repliedText = msg.ReplyToMessage?.Text ?? msg.ReplyToMessage?.Caption;
        if (!isReplyToBot && !string.IsNullOrWhiteSpace(repliedText))
        {
            var repliedSenderName = GetSenderName(msg.ReplyToMessage!.From);
            messages.Add(new UserChatMessage(string.IsNullOrEmpty(repliedSenderName)
                ? repliedText
                : $"[{repliedSenderName}]: {repliedText}"));
        }

        var senderName = GetSenderName(msg.From);
        if (!string.IsNullOrEmpty(senderName))
        {
            messages.Add(new SystemChatMessage(PromptLibrary.Get("Chat/CurrentSender", new { sender = senderName })));
        }

        var requestText = string.IsNullOrEmpty(senderName) ? request : $"[{senderName}]: {request}";
        var images = await DownloadImagesAsync(msg, сancellationToken);
        if (images.Count > 0)
        {
            messages.Add(new SystemChatMessage(PromptLibrary.Get("Chat/ImageAttached")));
            messages.Add(new UserChatMessage(images
                .Select(image =>
                    ChatMessageContentPart.CreateImagePart(new BinaryData(image), "image/jpeg",
                        ChatImageDetailLevel.Auto))
                .Append(ChatMessageContentPart.CreateTextPart(requestText))));
        }
        else
        {
            messages.Add(new UserChatMessage(requestText));
        }

        var toolContext = new ChatToolContext(msg, request, images);
        var tools = _tools.Concat(_chatToolFactory.Create(toolContext)).ToList();
        _logger.LogInformation("Chat generation context: agent {Agent}, sender {Sender}, images {ImageCount}",
            chatEntity.AiAgent?.Name ?? "none", senderName, images.Count);
        var aiResult = await _telegramBotClient.WithChatActionAsync(msg.Chat, ChatAction.Typing,
            () => _aiService.CompleteChatAsync(messages, tools, сancellationToken), сancellationToken);

        var rawResult = aiResult.Text;
        var result = rawResult.StripMarkdown();
        if (result != rawResult)
        {
            _logger.LogInformation("Markdown stripped from the answer: {Before} -> {After} chars", rawResult.Length,
                result.Length);
        }

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

    /// <summary>
    /// The history keeps only the text of past answers, so the model can't tell a real action from a claimed one
    /// and starts imitating «поставил напоминание» without calling the tool. Tell it what was actually called.
    /// </summary>
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