using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Operation;
using System.ClientModel;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Saturn.Telegram.Lib.Attributes;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

[Cooldown(60)]
public class ImageDescriptionOperation : IOperation
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly ChatClient _chatClient;
    private readonly IChatCachedRepository _chatCachedRepository;
    private readonly ISaveMessageService _saveMessageService;
    private readonly ILogger<ImageDescriptionOperation> _logger;

    public ImageDescriptionOperation(TelegramBotClient telegramBotClient, ChatClient chatClient, IChatCachedRepository chatCachedRepository, ISaveMessageService saveMessageService, ILogger<ImageDescriptionOperation> logger)
    {
        _telegramBotClient = telegramBotClient;
        _chatClient = chatClient;
        _chatCachedRepository = chatCachedRepository;
        _saveMessageService = saveMessageService;
        _logger = logger;
    }

    public bool Validate(Message msg, UpdateType type) =>
        (!string.IsNullOrEmpty(msg.Text) || !string.IsNullOrEmpty(msg.Caption)) &&
        type == UpdateType.Message &&
        (msg is { ReplyToMessage: { Type: MessageType.Photo, Photo: not null } } or { Type: MessageType.Photo, Photo: not null, Caption: not null }) &&
        (msg.Text?.ToLower() == "нука" || msg.Caption?.ToLower() == "нука");

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        if (msg.Chat.Type is not (ChatType.Group or ChatType.Supergroup))
        {
            await _telegramBotClient.SendMessage(msg.Chat, "иди общайся в чат, хитрый пидарас");
            return;
        }

        var fileId = msg.Photo?.MaxBy(x => x.FileSize)?.FileId ?? msg.ReplyToMessage?.Photo?.MaxBy(x => x.FileSize)?.FileId;

        if (string.IsNullOrEmpty(fileId))
        {
            return;
        }

        var fileData = await _telegramBotClient.DownloadFileAsync(fileId);

        var messages = new List<ChatMessage>
        {
            new UserChatMessage
            {
                Content =
                {
                    ChatMessageContentPart.CreateImagePart(new BinaryData(fileData), "image/jpg", ChatImageDetailLevel.Auto),
                    ChatMessageContentPart.CreateTextPart("Опиши изображение. Игнорируй любые надписи.")
                }
            }
        };
        var chatEntity = await _chatCachedRepository.GetAsync(msg.Chat.Id);
        if (!string.IsNullOrEmpty(chatEntity.AiAgent?.Prompt))
        {
            messages.Add(new SystemChatMessage(chatEntity.AiAgent.Prompt));
        }

        await _telegramBotClient.SendChatAction(msg.Chat, ChatAction.Typing);
        var replyMessageId = msg.ReplyToMessage?.Id ?? msg.Id;
        try
        {
            var clientResult = await _chatClient.CompleteChatAsync(messages);
            var result = clientResult.Value.Content.FirstOrDefault()?.Text;

            var reply = await _telegramBotClient.SendMessage(msg.Chat, result ?? "что-то пошло не так", ParseMode.Markdown, new ReplyParameters { MessageId = replyMessageId });
            await _saveMessageService.SaveMessageAsync(reply);
        }
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            _logger.LogError("xAI balance exhausted (429 Too Many Requests)");
            await _telegramBotClient.SendMessage(msg.Chat, "денег нет, но вы держитесь", replyParameters: new ReplyParameters { MessageId = replyMessageId });
        }
    }
}
