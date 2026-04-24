using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Operation;
using Saturn.Telegram.Lib.Attributes;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

[Cooldown(120)]
[GlobalCooldown(5)]
[ChatOnly("иди общайся в чат, хитрый пидарас")]
public class ImageEditOperation : IOperation
{
    private const string CommandPrefix1 = "отредактируй";
    private const string CommandPrefix2 = "измени";
    private const int MaxImages = 3;

    private readonly TelegramBotClient _telegramBotClient;
    private readonly IAiService _aiService;
    private readonly ISaveMessageService _saveMessageService;

    public ImageEditOperation(TelegramBotClient telegramBotClient, IAiService aiService, ISaveMessageService saveMessageService)
    {
        _telegramBotClient = telegramBotClient;
        _aiService = aiService;
        _saveMessageService = saveMessageService;
    }

    public bool Validate(Message msg, UpdateType type)
    {
        if (type != UpdateType.Message) return false;
        var text = msg.Text ?? msg.Caption;
        var hasPrefix = text?.StartsWith(CommandPrefix1, StringComparison.CurrentCultureIgnoreCase) == true ||
                        text?.StartsWith(CommandPrefix2, StringComparison.CurrentCultureIgnoreCase) == true;

        if (!hasPrefix) return false;

        if (msg.ReplyToMessage is { Type: MessageType.Photo, Photo: not null })
            return true;

        if (msg.Photo != null)
            return true;

        return false;
    }

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        var text = msg.Text ?? msg.Caption;
        var prefix = text!.StartsWith(CommandPrefix1, StringComparison.CurrentCultureIgnoreCase) ? CommandPrefix1 : CommandPrefix2;
        var prompt = text[prefix.Length..].Trim();

        var images = new List<byte[]>();

        if (msg.ReplyToMessage?.Photo != null)
        {
            var fileId = msg.ReplyToMessage.Photo.MaxBy(x => x.FileSize)!.FileId;
            images.Add(await _telegramBotClient.DownloadFileAsync(fileId));
        }

        if (msg.Photo != null)
        {
            var fileId = msg.Photo.MaxBy(x => x.FileSize)!.FileId;
            images.Add(await _telegramBotClient.DownloadFileAsync(fileId));
        }

        await ProcessEditAsync(msg, images.Take(MaxImages).ToList(), prompt);
    }

    private async Task ProcessEditAsync(Message msg, IReadOnlyList<byte[]> images, string prompt)
    {
        var editTask = _aiService.EditImageAsync(images, prompt);

        while (!editTask.IsCompleted)
        {
            await _telegramBotClient.SendChatAction(msg.Chat.Id, ChatAction.UploadPhoto);
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        var resultBytes = await editTask;

        using var resultStream = new MemoryStream(resultBytes);
        var reply = await _telegramBotClient.SendPhoto(
            msg.Chat.Id,
            new InputFileStream(resultStream),
            replyParameters: new ReplyParameters { MessageId = msg.MessageId });

        await _saveMessageService.SaveMessageAsync(reply);
    }
}
