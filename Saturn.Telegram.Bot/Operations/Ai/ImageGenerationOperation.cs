using Saturn.Bot.Service.Infrastructure.XaiImageGenerationClient;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

public class ImageGenerationOperation : IOperation
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly XaiImageGenerationClient _imageClient;

    public ImageGenerationOperation(TelegramBotClient telegramBotClient, XaiImageGenerationClient imageClient)
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

        var prompt = msg.Text.ToLower().Replace("сгенерируй ", string.Empty).Replace("покажи ", string.Empty);

        var generateTask = _imageClient.GenerateImageAsync(prompt);

        while (!generateTask.IsCompleted)
        {
            await _telegramBotClient.SendChatAction(msg.Chat.Id, ChatAction.UploadPhoto);
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        var result = await generateTask;

        using var generatedImage = new MemoryStream(result);
        await _telegramBotClient.SendPhoto(msg.Chat.Id, new InputFileStream(generatedImage), replyParameters: new ReplyParameters { MessageId = msg.MessageId });
    }

    public Task OnUpdateAsync(Update update) => Task.CompletedTask;
}
