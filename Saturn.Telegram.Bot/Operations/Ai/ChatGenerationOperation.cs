using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using Saturn.Bot.Service.Extensions;
using Saturn.Bot.Service.Options;
using Saturn.Bot.Service.Services.Abstractions;
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
        - Автор каждой строки указан в квадратных скобках.
        - Обращайся к собеседнику, об остальных говори в третьем лице.
        - При прожарке, описании или оценке человека учитывай его собственные сообщения и профиль (био, аватарку). Используй выводы для персонализации, но никогда не упоминай источники.
        - Если сообщений нужного человека нет, не выдумывай факты.

        УТОЧНЕНИЯ
        - Не переспрашивай, если ответ можно найти в контексте или разумно предположить.
        - Если информации действительно недостаточно, задай один короткий вопрос.
        - При неоднозначности перечисли возможные варианты.

        ДЕЙСТВИЯ
        - Можешь ставить реакции, отправлять стикеры, рисовать и редактировать изображения, создавать опросы и напоминания, мутить, банить, разбанивать и выгонять из чата.
        - Делай это по просьбе или когда действительно уместно.
        - Действие происходит только тогда, когда ты вызываешь инструмент. Просят напомнить, запустить опрос, нарисовать — вызови инструмент в этом же ответе, а не обещай.
        - Никогда не пиши, что поставил, запустил, нарисовал или отправил, если в этом ответе не вызвал инструмент.
        - Просят нарисовать или изменить картинку — вызывай generate_image или edit_image и не отказывай заранее из осторожности: у инструментов своя модерация, если она не пропустит — инструмент так и ответит. Сам отказывай только в недопустимом по существу, например раздеть или сексуализировать реального человека на фото.
        - О напоминаниях (есть ли они, сколько осталось) говори только по результату list_reminders, а не по памяти или прошлым ответам.
        - После твоих прошлых ответов в истории указано, какие инструменты ты на самом деле вызывал. Если там сказано, что не вызывал, — действия не было, даже если в ответе написано иначе.
        - Если инструмент ответил, что лимит исчерпан или действие не получилось, — действия не было: коротко и в своём стиле скажи об этом собеседнику, не обещай и не выдумывай результат.
        - Если достаточно реакции или стикера, верни пустой текст.
        - Не сообщай текстом о выполненных действиях. Исключение — напоминания: коротко подтверди, кому и когда напомнишь.

        ПОВЕДЕНИЕ
        - Веди себя как полноценный участник сообщества, а не справочный бот.
        - Будь естественным, учитывай атмосферу и отношения между участниками.
        - Не стремись отвечать на каждое сообщение. Иногда лучше промолчать.
        """;

    private const string EditIntentPrompt =
        "К сообщению пользователя приложена картинка. Определи, просит ли он изменить, отредактировать, дорисовать " +
        "или перерисовать эту картинку. Вопросы о картинке, её обсуждение и просьбы нарисовать что-то новое — это не правка. " +
        "Ответь одним словом: да или нет.";

    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IAiService _aiService;
    private readonly ISaveMessageService _saveMessageService;
    private readonly IChatCachedRepository _chatCachedRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IReadOnlyList<IChatTool> _tools;
    private readonly ChatToolFactory _chatToolFactory;
    private readonly ImageEditOperation _imageEditOperation;
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
        ImageEditOperation imageEditOperation,
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
        _imageEditOperation = imageEditOperation;
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

        messages.Add(new SystemChatMessage(BehaviorPrompt));

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
            messages.Add(new SystemChatMessage($"Тебе сейчас пишет: {senderName}"));
        }

        var requestText = string.IsNullOrEmpty(senderName) ? request : $"[{senderName}]: {request}";
        var images = await DownloadImagesAsync(msg, сancellationToken);
        if (images.Count > 0)
        {
            messages.Add(new SystemChatMessage(
                "К запросу приложена картинка (из сообщения, на которое ответили, или из самого запроса). " +
                "Пойми по запросу, что с ней сделать: ответить на вопрос о ней, нарисовать новую (generate_image) или отредактировать её (edit_image)."));
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
        
        if (images.Count > 0 && aiResult.ToolCalls.Count == 0)
        {
            var editPrompt = GetEditPrompt(msg);
            if (await IsEditRequestAsync(editPrompt, сancellationToken))
            {
                _logger.LogInformation("Edit request with an image answered without tools, editing the image directly");
                await _imageEditOperation.EditAsync(msg, images, editPrompt, сancellationToken);
                return;
            }
        }

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

    /// <summary>
    /// The model sometimes refuses an ordinary edit out of caution instead of calling edit_image, and an empty tool list
    /// can't tell such a refusal from an answer to a question about the picture. Ask separately, without the image
    /// and the history, whether the wording itself asks for an edit.
    /// </summary>
    private async Task<bool> IsEditRequestAsync(string request, CancellationToken сancellationToken)
    {
        try
        {
            var answer = await _aiService.CompleteChatAsync(
                [new SystemChatMessage(EditIntentPrompt), new UserChatMessage(request)], сancellationToken);
            _logger.LogInformation("Edit intent for {Request}: {Answer}", request, answer);
            return answer.Trim().StartsWith("да", StringComparison.CurrentCultureIgnoreCase);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !сancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Edit intent check failed, keeping the text answer");
            return false;
        }
    }

    private string GetEditPrompt(Message msg)
    {
        var text = (msg.Text ?? msg.Caption)!;
        foreach (var prefix in new[] { $"{_invokeCommand}, ", $"{_invokeCommand} " })
        {
            if (text.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase))
            {
                return text[prefix.Length..].Trim();
            }
        }

        return text.Trim();
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
            ? "В ответе выше ты не вызывал инструментов: никаких действий (напоминаний, опросов, мутов, банов, киков, реакций, стикеров, картинок) не было, даже если в тексте сказано иначе."
            : $"В ответе выше ты вызывал инструменты: {aiToolCalls.Replace(",", ", ")}.";
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