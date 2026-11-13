using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Saturn.Bot.Service.Extensions;
using Saturn.Bot.Service.Options;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Bot.Service.Services.Tools;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Db.Repositories.Abstractions;
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
        "Отвечай обычным текстом без markdown: без звёздочек, решёток, обратных кавычек и заголовков. " +
        "Всё, что узнал через инструменты (история чата, погода, Википедия и т.д.), подавай так, будто знал это и так: " +
        "не упоминай инструменты, поиск, историю чата, логи и переписку как источник: никаких «смотрю по истории», " +
        "«судя по истории чата», «я поискал», «я погрузился в твой чат-лог», «почитал твою переписку», " +
        "не спрашивай, видел ли кто-то историю. " +
        "Сообщения помечены автором в квадратных скобках. Твой собеседник — автор последнего сообщения; " +
        "люди, о которых он спрашивает или чьи сообщения ты нашёл, — другие участники чата, не путай их с ним. " +
        "Отвечай собеседнику, а о других говори в третьем лице, если тебя не просят обратиться к ним напрямую. " +
        "Когда просят что-то сделать с другим человеком (прожарить, описать, оценить), опирайся только на его собственные сообщения " +
        "(смотри на автора в каждой строке) и никогда не приписывай ему то, что писал собеседник или кто-то ещё; " +
        "если сообщений этого человека не нашлось — так и скажи или уточни, кого имели в виду. " +
        "Уточняющий вопрос задавай только когда без него ответить нельзя: не хватает важной информации " +
        "и её не выяснить из переписки выше или через инструменты (например, по истории чата). " +
        "Тогда вместо ответа задай один короткий вопрос, а если есть варианты — перечисли их. " +
        "Примеры: спрашивают погоду, а город неизвестен и в истории его не найти — спроси город; " +
        "поиск по истории нашёл несколько подходящих участников — перечисли их (имя и username) и спроси, кого имели в виду. " +
        "Если можно разумно предположить или вопрос и так понятен — отвечай сразу, не переспрашивай.";

    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IAiService _aiService;
    private readonly ISaveMessageService _saveMessageService;
    private readonly IChatCachedRepository _chatCachedRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IReadOnlyList<IChatTool> _tools;
    private readonly ILogger<ChatGenerationOperation> _logger;
    private readonly ILogger<ChatHistoryChatTool> _chatHistoryToolLogger;
    private readonly string _invokeCommand;
    private readonly string _botUsername;

    public ChatGenerationOperation(
        ITelegramBotClient telegramBotClient,
        IAiService aiService,
        ISaveMessageService saveMessageService,
        IChatCachedRepository chatCachedRepository,
        IMessageRepository messageRepository,
        IEnumerable<IChatTool> tools,
        IOptions<BotOptions> botOptions,
        ILogger<ChatGenerationOperation> logger,
        ILogger<ChatHistoryChatTool> chatHistoryToolLogger)
    {
        _telegramBotClient = telegramBotClient;
        _aiService = aiService;
        _saveMessageService = saveMessageService;
        _chatCachedRepository = chatCachedRepository;
        _messageRepository = messageRepository;
        _tools = tools.ToList();
        _logger = logger;
        _chatHistoryToolLogger = chatHistoryToolLogger;
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

        await _telegramBotClient.SendChatAction(msg.Chat, ChatAction.Typing, cancellationToken: сancellationToken);
        var tools = _tools.Append(new ChatHistoryChatTool(_messageRepository, msg.Chat.Id, _chatHistoryToolLogger)).ToList();
        _logger.LogInformation("Chat generation context: agent {Agent}, sender {Sender}, reply photo {HasPhoto}",
            chatEntity.AiAgent?.Name ?? "none", senderName, replyPhoto != null);
        var rawResult = await _aiService.CompleteChatAsync(messages, tools, сancellationToken);
        var result = rawResult.StripMarkdown();
        if (result != rawResult)
        {
            _logger.LogInformation("Markdown stripped from the answer: {Before} -> {After} chars", rawResult.Length, result.Length);
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
