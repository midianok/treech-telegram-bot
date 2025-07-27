using System.ClientModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Operation;
using Saturn.Telegram.Lib.Services.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

public class ImageDescriptionOperation : OperationBase
{
    private readonly ChatClient _chatClient;
    private readonly ISaveMessageService _saveMessageService;

    public ImageDescriptionOperation(ISaveMessageService saveMessageService, IConfiguration configuration)
    {
        var apiKey = configuration.GetSectionOrThrow("CHAT_GENERATION_API_KEY");
        _chatClient =  new ChatClient("grok-2-vision-latest", new ApiKeyCredential(apiKey), new OpenAIClientOptions { Endpoint = new Uri("https://api.x.ai/v1") });;
        _saveMessageService = saveMessageService;
    }

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        using var typingCancellationTokenSource = new CancellationTokenSource();
        _ = SendTypingAsync(msg.Chat.Id, typingCancellationTokenSource.Token);
        var fileId = msg.ReplyToMessage?.Photo?.MaxBy(x => x.FileSize)?.FileId;
        if (string.IsNullOrEmpty(fileId))
        {
            return;
        }

        var file = await TelegramBotClient.GetFile(fileId);
        if (string.IsNullOrEmpty(file.FilePath))
        {
            return;
        }

        using var fileData = new MemoryStream();
        await TelegramBotClient.DownloadFile(file.FilePath, fileData);

        var message = new UserChatMessage()
        {
            Content =
            {
                ChatMessageContentPart.CreateImagePart(new BinaryData(fileData.ToArray()), "image/jpg",
                    ChatImageDetailLevel.Auto),
                ChatMessageContentPart.CreateTextPart(
                    "Шутливо и смешно опиши что изображено на изображении. Всегда токсично, можно вульгарно, можно с матом, можно с двачерским сленгом. " +
                    "Если изображение имеет политический подтекст,  связанный с Украиной, надо ответить токсично как бы это ответил Дмитрий Медведев или Владимир Соловьёв. ")
            }
        };

        try
        {
            var clientResult = await _chatClient.CompleteChatAsync(message);
            var result = clientResult.Value.Content.FirstOrDefault()?.Text;

            await typingCancellationTokenSource.CancelAsync();
            var reply = await TelegramBotClient.SendMessage(msg.Chat, result ?? "что-то пошло не так", ParseMode.Markdown, new ReplyParameters { MessageId = msg.ReplyToMessage.Id });
            await _saveMessageService.SaveMessageAsync(reply);
        }
        catch (Exception e)
        {
            await TelegramBotClient.SendMessage(msg.Chat, "что-то пошло не так", ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id });
            await typingCancellationTokenSource.CancelAsync();
            Logger.LogError(e, e.Message);
        }
    }
    
    private async Task SendTypingAsync(long chatId, CancellationToken cancellationToken)
    {
        using var timeoutCancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        using var combinedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellationTokenSource.Token);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await TelegramBotClient.SendChatAction(chatId, ChatAction.Typing,
                    cancellationToken: combinedCancellationTokenSource.Token);
                await Task.Delay(TimeSpan.FromSeconds(5), combinedCancellationTokenSource.Token);
            }
        }
        catch (Exception)
        {
            // ignored
        }
    }
    
    protected override bool ValidateOnTextMessage(Message msg, UpdateType type)
    {
        return type == UpdateType.Message && 
               msg.ReplyToMessage!.Type == MessageType.Photo &&
               msg.ReplyToMessage?.Photo != null &&
               msg.Text?.ToLower() == "чоэта";
    }
}