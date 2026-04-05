using OpenAI.Images;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

public class ImageGenerationOperation : IOperation
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly ImageClient _imageClient;

    public ImageGenerationOperation(TelegramBotClient telegramBotClient, ImageClient imageClient)
    {
        _telegramBotClient = telegramBotClient;
        _imageClient = imageClient;
    }

    public bool Validate(Message msg, UpdateType type) =>
        !string.IsNullOrEmpty(msg.Text) && msg.Text.StartsWith("покажи ", StringComparison.CurrentCultureIgnoreCase);

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        if (msg.Chat.Type is not (ChatType.Group or ChatType.Supergroup))
        {
            await _telegramBotClient.SendMessage(msg.Chat, "иди общайся в чат, хитрый пидарас");
            return;
        }

        var request = msg.Text.ToLower().Replace("сгенерируй ", string.Empty).Replace("покажи ", string.Empty);
        var clientResult = _imageClient.GenerateImageAsync(request, new ImageGenerationOptions { ResponseFormat = GeneratedImageFormat.Bytes });

        while (!clientResult.IsCompleted)
        {
            await _telegramBotClient.SendChatAction(msg.Chat.Id, ChatAction.UploadPhoto);
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
        await Task.WhenAll(clientResult);
        var result = clientResult.Result.Value.ImageBytes.ToArray();

        using var generatedImage = new MemoryStream(result);
        await _telegramBotClient.SendPhoto(msg.Chat.Id, new InputFileStream(generatedImage), replyParameters: new ReplyParameters { MessageId = msg.MessageId });
    }

    public Task OnUpdateAsync(Update update) => Task.CompletedTask;
}
