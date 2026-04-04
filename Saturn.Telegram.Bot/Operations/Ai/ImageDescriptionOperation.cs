using OpenAI.Chat;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

public class ImageDescriptionOperation : OperationBase
{
    private readonly ChatClient _chatClient;
    private readonly ISaveMessageService _saveMessageService;

    public ImageDescriptionOperation(ChatClient chatClient, ISaveMessageService saveMessageService)
    {
        _chatClient =  chatClient;
        _saveMessageService = saveMessageService;
    }

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        if (msg.Chat.Type is not (ChatType.Group or ChatType.Supergroup))
        {
            await TelegramBotClient.SendMessage(msg.Chat, "иди общайся в чат, хитрый пидарас");
            return;
        }

        var fileId = msg.Photo?.MaxBy(x => x.FileSize)?.FileId ?? msg.ReplyToMessage?.Photo?.MaxBy(x => x.FileSize)?.FileId;
        
        if (string.IsNullOrEmpty(fileId))
        {
            return;
        }
        
        var fileData = await TelegramBotClient.DownloadFileAsync(fileId);

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

        await TelegramBotClient.SendChatAction(msg.Chat, ChatAction.Typing);
        var clientResult = await _chatClient.CompleteChatAsync(messages);
        var result = clientResult.Value.Content.FirstOrDefault()?.Text;
            
        var replyMessageId =  msg.ReplyToMessage?.Id ?? msg.Id;
            
        var reply = await TelegramBotClient.SendMessage(msg.Chat, result ?? "что-то пошло не так", ParseMode.Markdown, new ReplyParameters { MessageId = replyMessageId });
        await _saveMessageService.SaveMessageAsync(reply);
    }
    
    protected override bool ValidateMessage(Message msg, UpdateType type)
    {
        if (string.IsNullOrEmpty(msg.Text) && string.IsNullOrEmpty(msg.Caption))
        {
            return false;
        }

        return type == UpdateType.Message &&
               msg is { ReplyToMessage: { Type: MessageType.Photo, Photo: not null } } or { Type: MessageType.Photo, Photo: not null, Caption: not null } &&
               msg.Text?.ToLower() == "нука" || msg.Caption?.ToLower() == "нука";
    }
    
}