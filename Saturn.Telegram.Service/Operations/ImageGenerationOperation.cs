using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Images;
using Saturn.Bot.Service.Operations.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations;

public class ImageGenerationOperation : OperationBase
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly ImageClient _chatClient;

    public ImageGenerationOperation(ILogger<IOperation> logger, IConfiguration configuration, TelegramBotClient telegramBotClient) : base(logger, configuration)
    {
        _telegramBotClient = telegramBotClient;
        _chatClient = new ImageClient("dall-e-3", configuration.GetSection("OPEN_AI_KEY").Value);
    }

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var user = await _telegramBotClient.GetChatMember(msg.Chat.Id, msg.From!.Id);
        if (user.Status == ChatMemberStatus.Member)
        {
            await _telegramBotClient.SendMessage(msg.Chat.Id, "Только тричане с лычками могут генерировать пикчи", replyParameters: new ReplyParameters { MessageId = msg.MessageId } );
            return;
        }

        var request = msg.Text!.ToLower().Replace("сгенерируй ", string.Empty).Replace("покажи  ", string.Empty);
        var clientResult = _chatClient.GenerateImageAsync(request, new ImageGenerationOptions { ResponseFormat = GeneratedImageFormat.Bytes } );

        while (!clientResult.IsCompleted)
        {
            await _telegramBotClient.SendChatAction(msg.Chat.Id, ChatAction.UploadPhoto);
        }
        await Task.WhenAll(clientResult);
        var result = clientResult.Result.Value.ImageBytes.ToArray();

        using var generatedImage = new MemoryStream(result);
        await _telegramBotClient.SendPhoto(msg.Chat.Id, new InputFileStream(generatedImage), replyParameters: new ReplyParameters { MessageId = msg.MessageId } );
    }

    protected override bool ValidateOnMessage(Message msg, UpdateType type) =>
        type == UpdateType.Message &&
        !string.IsNullOrEmpty(msg.Text) &&
        (msg.Text.StartsWith("сгенерируй ", StringComparison.CurrentCultureIgnoreCase) || msg.Text.StartsWith("покажи ", StringComparison.CurrentCultureIgnoreCase));
}