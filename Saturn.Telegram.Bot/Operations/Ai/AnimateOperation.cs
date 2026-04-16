using Microsoft.Extensions.Logging;
using Saturn.Bot.Service.Infrastructure.XaiVideoGenerationClient;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Attributes;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Operation;
using System.Net;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

[Cooldown(300)]
[GlobalCooldown(3)]
public class AnimateOperation : IOperation
{
    private const string Command = "оживи";

    private readonly TelegramBotClient _telegramBotClient;
    private readonly XaiVideoGenerationClient _xaiVideoGenerationClient;
    private readonly ISaveMessageService _saveMessageService;
    private readonly ILogger<AnimateOperation> _logger;

    public AnimateOperation(
        TelegramBotClient telegramBotClient,
        XaiVideoGenerationClient xaiVideoGenerationClient,
        ISaveMessageService saveMessageService,
        ILogger<AnimateOperation> logger)
    {
        _telegramBotClient = telegramBotClient;
        _xaiVideoGenerationClient = xaiVideoGenerationClient;
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
        if (msg.Chat.Type is not (ChatType.Group or ChatType.Supergroup))
        {
            await _telegramBotClient.SendMessage(msg.Chat, "иди общайся в чат, хитрый пидарас");
            return;
        }

        var fileId = msg.Photo?.MaxBy(x => x.FileSize)?.FileId
            ?? msg.ReplyToMessage?.Photo?.MaxBy(x => x.FileSize)?.FileId;

        if (string.IsNullOrEmpty(fileId)) return;

        var imageBytes = await _telegramBotClient.DownloadFileAsync(fileId);

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        try
        {
            var generateTask = _xaiVideoGenerationClient.GenerateVideoFromImageAsync(imageBytes, cts.Token);

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
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.LogError("xAI balance exhausted (429 Too Many Requests)");
            await _telegramBotClient.SendMessage(msg.Chat.Id, "денег нет, но вы держитесь", replyParameters: new ReplyParameters { MessageId = msg.MessageId });
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

    public Task OnUpdateAsync(Update update) => Task.CompletedTask;
}
