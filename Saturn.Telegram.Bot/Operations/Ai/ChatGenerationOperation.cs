using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Saturn.Bot.Service.Extensions;
using Saturn.Bot.Service.Options;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Bot.Service.Services.Tools;
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
// ReSharper disable MethodSupportsCancellation

namespace Saturn.Bot.Service.Operations.Ai;

[GlobalCooldown(5)]
[ChatOnly("иди общайся в чат, хитрый пидарас")]
public class ChatGenerationOperation : IOperation
{
    private const string BehaviorPrompt =
        """
        ФОРМАТ
        - Обычный текст без Markdown, заголовков и форматирования.
        - Пиши естественно, кратко, без лишних пояснений.
        - Не описывай словами свои действия.
        
        КОНТЕКСТ
        - Используй историю чата, профили участников и внешнюю информацию, но никогда не раскрывай источники своих знаний.
        - Исключение: по запросу о конкретном сообщении предоставляй прямую ссылку на него.
        - Учитывай локальные шутки, мемы и предыдущие обсуждения.
        
        УЧАСТНИКИ
        - Автор последнего сообщения — твой собеседник. Автор каждой строки указан в квадратных скобках.
        - Не путай участников и не приписывай им чужие слова.
        - Обращайся к собеседнику, об остальных говори в третьем лице.
        - При прожарке, описании или оценке человека учитывай его собственные сообщения и профиль (био, аватарку). Используй выводы для персонализации, но никогда не упоминай источники.
        - Если сообщений нужного человека нет, не выдумывай факты.
        
        УТОЧНЕНИЯ
        - Не переспрашивай, если ответ можно найти в контексте или разумно предположить.
        - Если информации действительно недостаточно, задай один короткий вопрос.
        - При неоднозначности перечисли возможные варианты.
        
        ДЕЙСТВИЯ
        - Можешь ставить реакции, отправлять стикеры и изображения, создавать опросы и напоминания.
        - Делай это по просьбе или когда действительно уместно.
        - Если достаточно реакции или стикера, верни пустой текст.
        - Не сообщай текстом о выполненных действиях.
        
        ПОВЕДЕНИЕ
        - Веди себя как полноценный участник сообщества, а не справочный бот.
        - Будь естественным, учитывай атмосферу и отношения между участниками.
        - Не стремись отвечать на каждое сообщение. Иногда лучше промолчать.
        """;

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
        if (string.IsNullOrEmpty(msg.Text))
        {
            return false;
        }

        return msg.Text.StartsWith($"{_invokeCommand} ", StringComparison.CurrentCultureIgnoreCase) ||
               msg.Text.StartsWith($"{_invokeCommand}, ", StringComparison.CurrentCultureIgnoreCase) ||
               IsReplyToBot(msg);
    }

    public async Task OnMessageAsync(Message msg, UpdateType type, CancellationToken сancellationToken)
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
        messages.Add(new SystemChatMessage(BehaviorPrompt));

        var isReplyToBot = IsReplyToBot(msg);
        if (isReplyToBot)
        {
            var messageChain = await _messageRepository.GetMessageChainAsync(msg.Chat.Id, msg.ReplyToMessage!.Id);
            if (messageChain.Count > 0)
            {
                var chainMessages = messageChain.OrderBy(x => x.MessageDate)
                    .Select(x =>
                    {
                        if (x.IsBot)
                            return (ChatMessage)new AssistantChatMessage(x.Text ?? string.Empty);
                        var senderName = GetSenderName(x.User);
                        var text = string.IsNullOrEmpty(senderName) ? x.Text : $"[{senderName}]: {x.Text}";
                        return new UserChatMessage(text);
                    });
                messages.AddRange(chainMessages);
                _logger.LogInformation("Reply to bot: loaded chain of {Count} messages", messageChain.Count);
            }
            else
            {
                _logger.LogInformation("Reply to bot: message {MessageId} not found in DB, using only the replied text", msg.ReplyToMessage.Id);
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
        // Same "[sender]: text" marking as in the reply chain, so the model doesn't mix up the sender with people found in history
        var requestText = string.IsNullOrEmpty(senderName) ? request : $"[{senderName}]: {request}";
        var replyPhoto = msg.ReplyToMessage?.Photo?.MaxBy(x => x.FileSize);
        if (replyPhoto != null)
        {
            var imageBytes = await _telegramBotClient.DownloadFileAsync(replyPhoto.FileId, сancellationToken);
            messages.Add(new UserChatMessage(
                ChatMessageContentPart.CreateImagePart(new BinaryData(imageBytes), "image/jpeg", ChatImageDetailLevel.Auto),
                ChatMessageContentPart.CreateTextPart(requestText)));
        }
        else
        {
            messages.Add(new UserChatMessage(requestText));
        }

        var toolContext = new ChatToolContext(msg);
        var tools = _tools.Concat(_chatToolFactory.Create(toolContext)).ToList();
        _logger.LogInformation("Chat generation context: agent {Agent}, sender {Sender}, reply photo {HasPhoto}",
            chatEntity.AiAgent?.Name ?? "none", senderName, replyPhoto != null);
        // Tool calls make the answer take well over the 5 seconds a single "typing" action lasts
        var rawResult = await _telegramBotClient.WithChatActionAsync(msg.Chat, ChatAction.Typing,
            () => _aiService.CompleteChatAsync(messages, tools, сancellationToken), сancellationToken);
        var result = rawResult.StripMarkdown();
        if (result != rawResult)
        {
            _logger.LogInformation("Markdown stripped from the answer: {Before} -> {After} chars", rawResult.Length, result.Length);
        }

        if (string.IsNullOrWhiteSpace(result))
        {
            if (!toolContext.ActionTaken)
            {
                throw new AiEmptyResponseException();
            }

            // The model answered with a reaction, sticker or the like and had nothing to add
            _logger.LogInformation("Empty text answer after a chat action, sending nothing");
            return;
        }

        var reply = await _telegramBotClient.SendMessage(msg.Chat, result, ParseMode.None, new ReplyParameters { MessageId = msg.Id }, cancellationToken: сancellationToken);
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
        if (string.IsNullOrEmpty(_botUsername) || msg.ReplyToMessage == null || msg.ReplyToMessage.From == null)
        {
            return false;
        }
        return msg.ReplyToMessage.Type == MessageType.Text && msg.ReplyToMessage.From.Username == _botUsername;
    }
}
