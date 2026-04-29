using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.FunnyStaff;

public class OlegexOperation : IOperation
{
    private const string TargetUsername = "Olegex3";
    private const double TriggerProbability = 0.1;

    private static readonly string[] VideoExtensions = [".mp4", ".mov", ".avi"];
    private static readonly string[] PhotoExtensions = [".jpg", ".jpeg", ".png", ".gif"];

    private readonly TelegramBotClient _telegramBotClient;
    private readonly Random _random = new();
    private readonly string[] _mediaFiles;

    public OlegexOperation(TelegramBotClient telegramBotClient)
    {
        _telegramBotClient = telegramBotClient;
        var mediaDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Media");
        _mediaFiles = Directory.Exists(mediaDir) ? Directory.GetFiles(mediaDir) : [];
    }

    public bool Validate(Message msg, UpdateType type) =>
        type == UpdateType.Message &&
        msg.From?.Username?.Equals(TargetUsername, StringComparison.OrdinalIgnoreCase) == true &&
        _mediaFiles.Length > 0 &&
        _random.NextDouble() < TriggerProbability;

    public async Task OnMessageAsync(Message msg, UpdateType type)
    {
        var filePath = _mediaFiles[_random.Next(_mediaFiles.Length)];
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        await using var stream = File.OpenRead(filePath);
        var inputFile = new InputFileStream(stream, Path.GetFileName(filePath));

        if (Array.IndexOf(VideoExtensions, ext) >= 0)
        {
            await _telegramBotClient.SendVideo(msg.Chat.Id, inputFile,
                replyParameters: new ReplyParameters { MessageId = msg.Id });
        }
        else if (Array.IndexOf(PhotoExtensions, ext) >= 0)
        {
            await _telegramBotClient.SendPhoto(msg.Chat.Id, inputFile,
                replyParameters: new ReplyParameters { MessageId = msg.Id });
        }
    }
}
