using Saturn.Bot.Service.Extensions;
using OpenAI.Images;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Operation;
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
    private readonly IAiService _aiService;

    public ImageGenerationOperation(TelegramBotClient telegramBotClient, IAiService aiService)
    {
        _telegramBotClient = telegramBotClient;
        _aiService = aiService;
    }

    public bool Validate(Message msg, UpdateType type) =>
        msg.TextStartsWith("покажи");

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        var request = msg.Text.ToLower().Replace("покажи", string.Empty);

        var generationTask = _aiService.GenerateImageAsync(request, new ImageGenerationOptions { ResponseFormat = GeneratedImageFormat.Bytes });

        while (!generationTask.IsCompleted)
        {
            await _telegramBotClient.SendChatAction(msg.Chat.Id, ChatAction.UploadPhoto);
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        var generatedImage = await generationTask;
        var result = generatedImage.ImageBytes!.ToArray();

        using var generatedStream = new MemoryStream(result);
        await _telegramBotClient.SendPhoto(msg.Chat.Id, new InputFileStream(generatedStream), replyParameters: new ReplyParameters { MessageId = msg.MessageId });
    }
}
