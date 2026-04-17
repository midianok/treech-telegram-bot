using Microsoft.Extensions.Logging;
using Saturn.Bot.Service.Infrastructure.XaiImageEditClient;
using Saturn.Telegram.Lib;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Operation;
using System.Net;
using Saturn.Telegram.Lib.Attributes;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

[Cooldown(120)]
[GlobalCooldown(5)]
public class ImageEditOperation : IOperation
{
    private const string CommandPrefix1 = "отредактируй";
    private const string CommandPrefix2 = "измени";
    private const int MaxImages = 3;

    private readonly TelegramBotClient _telegramBotClient;
    private readonly XaiImageEditClient _xaiImageEditClient;
    private readonly ISaveMessageService _saveMessageService;
    private readonly ILogger<ImageEditOperation> _logger;

    public ImageEditOperation(TelegramBotClient telegramBotClient, XaiImageEditClient xaiImageEditClient, ISaveMessageService saveMessageService, ILogger<ImageEditOperation> logger)
    {
        _telegramBotClient = telegramBotClient;
        _xaiImageEditClient = xaiImageEditClient;
        _saveMessageService = saveMessageService;
        _logger = logger;
    }

    public bool Validate(Message msg, UpdateType type)
    {
        if (type != UpdateType.Message) return false;
        var text = msg.Text ?? msg.Caption;
        var hasPrefix = text?.StartsWith(CommandPrefix1, StringComparison.CurrentCultureIgnoreCase) == true ||
                        text?.StartsWith(CommandPrefix2, StringComparison.CurrentCultureIgnoreCase) == true;

        if (!hasPrefix) return false;

        // Reply to a photo (existing behaviour)
        if (msg.ReplyToMessage is { Type: MessageType.Photo, Photo: not null })
            return true;

        // Own message with photo(s) attached
        if (msg.Photo != null)
            return true;

        return false;
    }

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        if (msg.Chat.Type is not (ChatType.Group or ChatType.Supergroup))
        {
            await _telegramBotClient.SendMessage(msg.Chat, "иди общайся в чат, хитрый пидарас");
            return;
        }

        var text = msg.Text ?? msg.Caption;
        var prefix = text!.StartsWith(CommandPrefix1, StringComparison.CurrentCultureIgnoreCase) ? CommandPrefix1 : CommandPrefix2;
        var prompt = text[prefix.Length..].Trim();

        var images = new List<byte[]>();

        // Photos from the replied-to message
        if (msg.ReplyToMessage?.Photo != null)
        {
            var fileId = msg.ReplyToMessage.Photo.MaxBy(x => x.FileSize)!.FileId;
            images.Add(await _telegramBotClient.DownloadFileAsync(fileId));
        }

        // Photos attached to the user's own message
        if (msg.Photo != null)
        {
            var fileId = msg.Photo.MaxBy(x => x.FileSize)!.FileId;
            images.Add(await _telegramBotClient.DownloadFileAsync(fileId));
        }

        await ProcessEditAsync(msg, images.Take(MaxImages).ToList(), prompt);
    }

    private async Task ProcessEditAsync(Message msg, IReadOnlyList<byte[]> images, string prompt)
    {
        try
        {
            var editTask = _xaiImageEditClient.EditImageAsync(images, prompt);

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
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.LogError("xAI balance exhausted (429 Too Many Requests)");
            await _telegramBotClient.SendMessage(msg.Chat.Id, "денег нет, но вы держитесь", replyParameters: new ReplyParameters { MessageId = msg.MessageId });
        }
    }
}
