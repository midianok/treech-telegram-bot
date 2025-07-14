using System.ClientModel;
using System.Globalization;
using Humanizer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Images;
using Saturn.Telegram.Db.Entities;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

public class ImageGenerationOperation : OperationBase
{
    protected override bool CooldownNeeded => true;
    protected override SubscriptionType SubscriptionType => SubscriptionType.RemoveImageGenerationCooldown;
    
    private readonly ImageClient _imageClient;
    public ImageGenerationOperation(IConfiguration configuration)
    {
        _imageClient = new ImageClient("grok-2-image", new ApiKeyCredential(configuration.GetSection("OPEN_AI_KEY").Value), new OpenAIClientOptions { Endpoint = new Uri("https://api.x.ai/v1") });
    }
    
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