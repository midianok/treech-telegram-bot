using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Operation;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Saturn.Telegram.Lib.Attributes;
using Saturn.Telegram.Lib.Infrastructure.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

[Cooldown(60)]
[ChatOnly("иди общайся в чат, хитрый пидарас")]
public class ImageDescriptionOperation : IOperation
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly IAiService _aiService;
    private readonly IChatCachedRepository _chatCachedRepository;
    private readonly ISaveMessageService _saveMessageService;

    public ImageDescriptionOperation(TelegramBotClient telegramBotClient, IAiService aiService, IChatCachedRepository chatCachedRepository, ISaveMessageService saveMessageService)
    {
        _telegramBotClient = telegramBotClient;
        _aiService = aiService;
        _chatCachedRepository = chatCachedRepository;
        _saveMessageService = saveMessageService;
    }

    public bool Validate(Message msg, UpdateType type) =>
        (!string.IsNullOrEmpty(msg.Text) || !string.IsNullOrEmpty(msg.Caption)) &&
        type == UpdateType.Message &&
        (msg is { ReplyToMessage: { Type: MessageType.Photo, Photo: not null } } or { Type: MessageType.Photo, Photo: not null, Caption: not null }) &&
        (msg.Text?.ToLower() == "нука" || msg.Caption?.ToLower() == "нука");

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
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
        var result = await _aiService.CompleteChatAsync(messages);

        var reply = await _telegramBotClient.SendMessage(msg.Chat, result, ParseMode.Markdown, new ReplyParameters { MessageId = replyMessageId });
        await _saveMessageService.SaveMessageAsync(reply);
    }
}
