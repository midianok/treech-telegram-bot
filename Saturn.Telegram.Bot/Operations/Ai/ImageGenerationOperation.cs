using Saturn.Bot.Service.Extensions;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Saturn.Telegram.Lib.Operation;
using Saturn.Telegram.Lib.Attributes;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

[Cooldown(10 * 60)]
[GlobalCooldown(2)]
[ChatOnly("иди общайся в чат, хитрый пидарас")]
public class ImageGenerationOperation : IOperation
{
    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IAiService _aiService;
    private readonly IImagePromptRepository _imagePromptRepository;

    public ImageGenerationOperation(ITelegramBotClient telegramBotClient, IAiService aiService, IImagePromptRepository imagePromptRepository)
    {
        _telegramBotClient = telegramBotClient;
        _aiService = aiService;
        _imagePromptRepository = imagePromptRepository;
    }

    public bool Validate(Message msg, UpdateType type) =>
        msg.TextStartsWith("покажи");

    public async Task OnMessageAsync(Message msg, UpdateType type, CancellationToken сancellationToken)
    {
        var rawQuery = msg.Text!.ToLower().Replace("покажи", string.Empty).Trim();
        var prompt = await _imagePromptRepository.FindPromptAsync(rawQuery) ?? rawQuery;

        var generationTask = _aiService.GenerateImageAsync(prompt);

        while (!generationTask.IsCompleted)
        {
            await _telegramBotClient.SendChatAction(msg.Chat.Id, ChatAction.UploadPhoto);
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        var result = await generationTask;

        using var generatedStream = new MemoryStream(result);
        await _telegramBotClient.SendPhoto(msg.Chat.Id, new InputFileStream(generatedStream), replyParameters: new ReplyParameters { MessageId = msg.MessageId });
    }
}
