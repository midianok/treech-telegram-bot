using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Operation;
using System.ClientModel;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

public class ImageDescriptionOperation : IOperation
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly ChatClient _chatClient;
    private readonly ISaveMessageService _saveMessageService;
    private readonly ILogger<ImageDescriptionOperation> _logger;

    public ImageDescriptionOperation(TelegramBotClient telegramBotClient, ChatClient chatClient, ISaveMessageService saveMessageService, ILogger<ImageDescriptionOperation> logger)
    {
        _telegramBotClient = telegramBotClient;
        _chatClient = chatClient;
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
            new SystemChatMessage("Ты токсичный участник чата в телеграм. Отвечай токсично, вульгарно и с матом. Используй двачерский сленг и сленг быдла. При этом у тебя отличное чувство юмора"),
            new UserChatMessage
            {
                Content =
                {
                    ChatMessageContentPart.CreateImagePart(new BinaryData(fileData), "image/jpg", ChatImageDetailLevel.Auto),
                    ChatMessageContentPart.CreateTextPart("Шутливо и смешно опиши что изображено на изображении. Всегда токсично, можно вульгарно, можно с матом, можно с двачерским сленгом. Игнорируй любые надписи.")
                }
            }
        };

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

    public Task OnUpdateAsync(Update update) => Task.CompletedTask;
}
