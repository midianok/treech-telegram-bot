using OpenAI.Images;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

public class ImageGenerationOperation : OperationBase
{
    private readonly ImageClient _imageClient;
    public ImageGenerationOperation(ImageClient imageClient) => 
        _imageClient = imageClient;

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var request = msg.Text!.ToLower().Replace("сгенерируй ", string.Empty).Replace("покажи ", string.Empty);
        var clientResult = _imageClient.GenerateImageAsync(request, new ImageGenerationOptions { ResponseFormat = GeneratedImageFormat.Bytes } );

        while (!clientResult.IsCompleted)
        {
            await TelegramBotClient.SendChatAction(msg.Chat.Id, ChatAction.UploadPhoto);
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
        await Task.WhenAll(clientResult);
        var result = clientResult.Result.Value.ImageBytes.ToArray();

        using var generatedImage = new MemoryStream(result);
        await TelegramBotClient.SendPhoto(msg.Chat.Id, new InputFileStream(generatedImage), replyParameters: new ReplyParameters { MessageId = msg.MessageId } );
    }

    protected override bool ValidateOnTextMessage(Message msg, UpdateType type) =>
        msg.Text!.StartsWith("сгенерируй ", StringComparison.CurrentCultureIgnoreCase) || msg.Text.StartsWith("покажи ", StringComparison.CurrentCultureIgnoreCase);
}