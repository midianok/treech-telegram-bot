using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Saturn.Bot.Service.Options;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Attributes;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Infrastructure.Abstractions;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

[ChatOnly("иди общайся в чат, хитрый пидарас")]
public class AnimateOperation : IOperation
{
    private const string Command = "оживи";
    private const string CommandWithAudio = "оживи со звуком";

    private readonly TelegramBotClient _telegramBotClient;
    private readonly IAiService _aiService;
    private readonly ISaveMessageService _saveMessageService;
    private readonly ILogger<AnimateOperation> _logger;
    private readonly BotOptions _botOptions;

    public AnimateOperation(
        TelegramBotClient telegramBotClient,
        IAiService aiService,
        ISaveMessageService saveMessageService,
        ILogger<AnimateOperation> logger,
        IOptions<BotOptions> botOptions)
    {
        _telegramBotClient = telegramBotClient;
        _aiService = aiService;
        _saveMessageService = saveMessageService;
        _logger = logger;
        _botOptions = botOptions.Value;
    }

    private static readonly (string ratio, double value)[] AspectRatios =
    [
        ("21:9", 21.0 / 9),
        ("16:9", 16.0 / 9),
        ("4:3",  4.0  / 3),
        ("1:1",  1.0),
        ("3:4",  3.0  / 4),
        ("9:16", 9.0  / 16)
    ];

    private static string DetectAspectRatio(int width, int height)
    {
        var ratio = (double)width / height;
        return AspectRatios.MinBy(x => Math.Abs(x.value - ratio)).ratio;
    }

    public bool Validate(Message msg, UpdateType type)
    {
        if (type != UpdateType.Message) return false;

        if (string.IsNullOrEmpty(_botOptions.AdminUsername) || !string.Equals(msg.From?.Username, _botOptions.AdminUsername, StringComparison.OrdinalIgnoreCase))
            return false;

        var text = msg.Text ?? msg.Caption;
        if (text == null) return false;

        var trimmed = text.Trim();
        if (!trimmed.StartsWith(Command, StringComparison.OrdinalIgnoreCase)) return false;

        if (msg.ReplyToMessage is { Type: MessageType.Photo, Photo: not null }) return true;
        if (msg.Photo != null) return true;

        return false;
    }

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        var photo = msg.Photo?.MaxBy(x => x.FileSize)
            ?? msg.ReplyToMessage?.Photo?.MaxBy(x => x.FileSize);

        if (photo == null) return;

        var text = (msg.Text ?? msg.Caption)?.Trim() ?? string.Empty;
        var withAudio = text.StartsWith(CommandWithAudio, StringComparison.OrdinalIgnoreCase);
        var activeCommand = withAudio ? CommandWithAudio : Command;
        var customPrompt = text.Length > activeCommand.Length
            ? text[activeCommand.Length..].Trim()
            : null;

        var aspectRatio = DetectAspectRatio(photo.Width, photo.Height);
        var imageBytes = await _telegramBotClient.DownloadFileAsync(photo.FileId);

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        try
        {
            var generateTask = _aiService.GenerateVideoFromImageAsync(imageBytes, customPrompt, aspectRatio, withAudio, cts.Token);

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
