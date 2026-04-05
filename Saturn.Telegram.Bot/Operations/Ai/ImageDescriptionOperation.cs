using Saturn.Bot.Service.Infrastructure.XaiChatClient;
using Saturn.Bot.Service.Infrastructure.XaiChatClient.Model;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

public class ImageDescriptionOperation : IOperation
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly XaiChatClient _chatClient;
    private readonly ISaveMessageService _saveMessageService;

    public ImageDescriptionOperation(TelegramBotClient telegramBotClient, XaiChatClient chatClient, ISaveMessageService saveMessageService)
    {
        _telegramBotClient = telegramBotClient;
        _chatClient = chatClient;
        _saveMessageService = saveMessageService;
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

        var messages = new List<XaiMessage>
        {
            XaiMessage.System("Ты токсичный участник чата в телеграм. Отвечай токсично, вульгарно и с матом. Используй двачерский сленг и сленг быдла. При этом у тебя отличное чувство юмора"),
            XaiMessage.UserWithImage(fileData, "Шутливо и смешно опиши что изображено на изображении. Всегда токсично, можно вульгарно, можно с матом, можно с двачерским сленгом. Игнорируй любые надписи.")
        };

        await _telegramBotClient.SendChatAction(msg.Chat, ChatAction.Typing);
        var result = await _chatClient.CompleteChatAsync(messages);

        var replyMessageId = msg.ReplyToMessage?.Id ?? msg.Id;

        var reply = await _telegramBotClient.SendMessage(msg.Chat, result ?? "что-то пошло не так", ParseMode.Markdown, new ReplyParameters { MessageId = replyMessageId });
        await _saveMessageService.SaveMessageAsync(reply);
    }

    public Task OnUpdateAsync(Update update) => Task.CompletedTask;
}
