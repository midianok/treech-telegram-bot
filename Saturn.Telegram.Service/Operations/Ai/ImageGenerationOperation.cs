using System.Globalization;
using Humanizer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using OpenAI.Images;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

public class ImageGenerationOperation : OperationBase
{
    protected override bool CooldownNeeded => true;
    
    private readonly ImageClient _imageClient;
    public ImageGenerationOperation(IConfiguration configuration)
    {
        _imageClient = new ImageClient("dall-e-3", configuration.GetSection("OPEN_AI_KEY").Value);
    }
    
    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var user = await TelegramBotClient.GetChatMember(msg.Chat.Id, msg.From!.Id);
        if (user.Status == ChatMemberStatus.Member && msg.From.Username != "nkess")
        {
            await TelegramBotClient.SendMessage(msg.Chat.Id, "Только тричане с лычками и Любимая Настя могут генерировать пикчи", replyParameters: new ReplyParameters { MessageId = msg.MessageId } );
            return;
        }
        if (MemoryCache.TryGetValue(msg.From.Id, out DateTime cooldownTime))
        {
            var elapsed = (cooldownTime - DateTime.Now).Humanize(2, culture: new CultureInfo("ru-RU"), collectionSeparator: " ");
            await TelegramBotClient.SendMessage(msg.Chat.Id, $"Отдохни ещё {elapsed}", replyParameters: new ReplyParameters { MessageId = msg.MessageId } );
            return;
        }
        MemoryCache.Set(msg.From.Id, DateTime.Now.AddMinutes(5), TimeSpan.FromMinutes(5));
        
        var request = msg.Text!.ToLower().Replace("сгенерируй ", string.Empty).Replace("покажи  ", string.Empty);
        
        
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

    protected override bool ValidateOnMessage(Message msg, UpdateType type) =>
        type.IsMessage() &&
        !string.IsNullOrEmpty(msg.Text) &&
        (msg.Text.StartsWith("сгенерируй ", StringComparison.CurrentCultureIgnoreCase) || msg.Text.StartsWith("покажи ", StringComparison.CurrentCultureIgnoreCase));
}