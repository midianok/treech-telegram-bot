using Saturn.Bot.Service.Extensions;
using Microsoft.Extensions.Logging;
using OpenAI.Images;
using Saturn.Telegram.Lib.Operation;
using System.ClientModel;
using Saturn.Telegram.Lib.Attributes;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

[Cooldown(120)]
[GlobalCooldown(5)]
[ChatOnly("иди общайся в чат, хитрый пидарас")]
public class ImageGenerationOperation : IOperation
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly ImageClient _imageClient;
    private readonly ILogger<ImageGenerationOperation> _logger;

    public ImageGenerationOperation(TelegramBotClient telegramBotClient, ImageClient imageClient, ILogger<ImageGenerationOperation> logger)
    {
        _telegramBotClient = telegramBotClient;
        _imageClient = imageClient;
        _logger = logger;
    }

    public bool Validate(Message msg, UpdateType type) =>
        msg.TextStartsWith("покажи ");

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        var request = msg.Text.ToLower().Replace("сгенерируй ", string.Empty).Replace("покажи ", string.Empty);
        try
        {
            var clientResult = _imageClient.GenerateImageAsync(request);

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
        catch (ClientResultException ex) when (ex.Status == 429)
        {
            _logger.LogError("xAI balance exhausted (429 Too Many Requests)");
            await _telegramBotClient.SendMessage(msg.Chat.Id, "денег нет, но вы держитесь", replyParameters: new ReplyParameters { MessageId = msg.MessageId });
        }
    }
}
