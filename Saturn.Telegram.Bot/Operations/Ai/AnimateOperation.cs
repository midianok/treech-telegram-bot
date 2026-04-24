using Microsoft.Extensions.Logging;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Attributes;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Infrastructure.Abstractions;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

[Allow(198607451)] //ilya_naprimer
[ChatOnly("иди общайся в чат, хитрый пидарас")]
public class AnimateOperation : IOperation
{
    private const string Command = "оживи";

    private readonly TelegramBotClient _telegramBotClient;
    private readonly IAiService _aiService;
    private readonly ISaveMessageService _saveMessageService;
    private readonly ILogger<AnimateOperation> _logger;

    public AnimateOperation(
        TelegramBotClient telegramBotClient,
        IAiService aiService,
        ISaveMessageService saveMessageService,
        ILogger<AnimateOperation> logger)
    {
        _telegramBotClient = telegramBotClient;
        _aiService = aiService;
        _saveMessageService = saveMessageService;
        _logger = logger;
    }

    public bool Validate(Message msg, UpdateType type)
    {
        if (type != UpdateType.Message) return false;

        var text = msg.Text ?? msg.Caption;
        if (!string.Equals(text?.Trim(), Command, StringComparison.CurrentCultureIgnoreCase)) return false;

        if (msg.ReplyToMessage is { Type: MessageType.Photo, Photo: not null }) return true;
        if (msg.Photo != null) return true;

        return false;
    }

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        var fileId = msg.Photo?.MaxBy(x => x.FileSize)?.FileId
            ?? msg.ReplyToMessage?.Photo?.MaxBy(x => x.FileSize)?.FileId;

        if (string.IsNullOrEmpty(fileId)) return;

        var imageBytes = await _telegramBotClient.DownloadFileAsync(fileId);

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        try
        {
            var generateTask = _aiService.GenerateVideoFromImageAsync(imageBytes, cts.Token);

            while (!generateTask.IsCompleted)
            {
                await _telegramBotClient.SendChatAction(msg.Chat.Id, ChatAction.UploadVideo, cancellationToken: cts.Token);
                await Task.Delay(TimeSpan.FromSeconds(4));
            }

            var videoBytes = await generateTask;

            using var videoStream = new MemoryStream(videoBytes);
            var reply = await _telegramBotClient.SendVideo(
                msg.Chat.Id,
                new InputFileStream(videoStream, "animate.mp4"),
                replyParameters: new ReplyParameters { MessageId = msg.MessageId });

            await _saveMessageService.SaveMessageAsync(reply);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Video generation failed");
            await _telegramBotClient.SendMessage(msg.Chat.Id, "не смог оживить, попробуй ещё раз", replyParameters: new ReplyParameters { MessageId = msg.MessageId });
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Video generation timed out");
            await _telegramBotClient.SendMessage(msg.Chat.Id, "слишком долго генерировал, сдался", replyParameters: new ReplyParameters { MessageId = msg.MessageId });
        }
    }
}
