using Saturn.Bot.Service.Infrastructure.XaiImageEditClient;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

public class ImageEditOperation : OperationBase
{
    private const string CommandPrefix1 = "отредактируй";
    private const string CommandPrefix2 = "измени";

    private readonly XaiImageEditClient _xaiImageEditClient;
    private readonly ISaveMessageService _saveMessageService;

    public ImageEditOperation(XaiImageEditClient xaiImageEditClient, ISaveMessageService saveMessageService)
    {
        _xaiImageEditClient = xaiImageEditClient;
        _saveMessageService = saveMessageService;
    }

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        if (msg.Chat.Type is not (ChatType.Group or ChatType.Supergroup))
        {
            await TelegramBotClient.SendMessage(msg.Chat, "иди общайся в чат, хитрый пидарас");
            return;
        }

        var text = msg.Text ?? msg.Caption ?? string.Empty;
        var prefix = text.StartsWith(CommandPrefix1, StringComparison.CurrentCultureIgnoreCase) ? CommandPrefix1 : CommandPrefix2;
        var prompt = text[prefix.Length..].Trim();

        var fileId = msg.ReplyToMessage!.Photo!.MaxBy(x => x.FileSize)!.FileId;
        var imageBytes = await TelegramBotClient.DownloadFileAsync(fileId);

        var editTask = _xaiImageEditClient.EditImageAsync(imageBytes, prompt);

        while (!editTask.IsCompleted)
        {
            await TelegramBotClient.SendChatAction(msg.Chat.Id, ChatAction.UploadPhoto);
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        var resultBytes = await editTask;

        using var resultStream = new MemoryStream(resultBytes);
        var reply = await TelegramBotClient.SendPhoto(
            msg.Chat.Id,
            new InputFileStream(resultStream),
            replyParameters: new ReplyParameters { MessageId = msg.MessageId });

        await _saveMessageService.SaveMessageAsync(reply);
    }

    protected override bool ValidateMessage(Message msg, UpdateType type)
    {
        var text = msg.Text ?? msg.Caption;
        return type == UpdateType.Message &&
               msg.ReplyToMessage is { Type: MessageType.Photo, Photo: not null } &&
               (text?.StartsWith(CommandPrefix1, StringComparison.CurrentCultureIgnoreCase) == true ||
                text?.StartsWith(CommandPrefix2, StringComparison.CurrentCultureIgnoreCase) == true);
    }
}
