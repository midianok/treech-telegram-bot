using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Saturn.Bot.Service.Options;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Saturn.Telegram.Lib.Extensions;
using Saturn.Telegram.Lib.Operation;
using Saturn.Telegram.Lib.Attributes;
using Saturn.Telegram.Lib.Infrastructure.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

[ChatOnly("иди общайся в чат, хитрый пидарас")]
public class ImageEditOperation : IOperation
{
    private const string CommandPrefix1 = "отредактируй";
    private const string CommandPrefix2 = "измени";
    private const int MaxImages = 3;

    // Free tier: one edit per user per hour. Hitting the limit opens the paywall.
    private static readonly TimeSpan FreeEditPeriod = TimeSpan.FromHours(1);

    private readonly ITelegramBotClient _telegramBotClient;
    private readonly IAiService _aiService;
    private readonly ISaveMessageService _saveMessageService;
    private readonly ICoinRepository _coinRepository;
    private readonly ISparkShop _sparkShop;
    private readonly IMemoryCache _memoryCache;
    private readonly string? _adminUsername;

    public ImageEditOperation(
        ITelegramBotClient telegramBotClient,
        IAiService aiService,
        ISaveMessageService saveMessageService,
        ICoinRepository coinRepository,
        ISparkShop sparkShop,
        IMemoryCache memoryCache,
        IOptions<BotOptions> botOptions)
    {
        _telegramBotClient = telegramBotClient;
        _aiService = aiService;
        _saveMessageService = saveMessageService;
        _coinRepository = coinRepository;
        _sparkShop = sparkShop;
        _memoryCache = memoryCache;
        _adminUsername = botOptions.Value.AdminUsername;
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

    public async Task OnMessageAsync(Message msg, UpdateType type, CancellationToken сancellationToken)
    {
        if (msg.From == null) return;

        var text = msg.Text ?? msg.Caption;
        var prefix = text!.StartsWith(CommandPrefix1, StringComparison.CurrentCultureIgnoreCase) ? CommandPrefix1 : CommandPrefix2;
        var prompt = text[prefix.Length..].Trim();
        var userId = msg.From.Id;

        // Admin: unlimited and free.
        if (IsAdmin(msg))
        {
            await RunEditAsync(msg, prompt, сancellationToken);
            return;
        }

        // Free tier: one edit per hour. A failed edit must not burn the quota.
        if (TryConsumeFreeEdit(userId))
        {
            try
            {
                await RunEditAsync(msg, prompt, сancellationToken);
            }
            catch
            {
                ReleaseFreeEdit(userId);
                throw;
            }
            return;
        }

        // Paid tier: charge coins, refund on any failure (moderation, timeout, budget...).
        if (await _coinRepository.TryChargeAsync(userId, _sparkShop.ImageEditCost, nameof(ImageEditOperation), сancellationToken))
        {
            try
            {
                await RunEditAsync(msg, prompt, сancellationToken);
            }
            catch
            {
                await _coinRepository.RefundAsync(userId, _sparkShop.ImageEditCost, nameof(ImageEditOperation), сancellationToken);
                throw;
            }
            return;
        }

        // No free quota and not enough coins: offer to top up.
        await _sparkShop.SendOfferAsync(msg, "Бесплатное редактирование на этот час использовано, а трич койнов не хватает.", сancellationToken);
    }

    private async Task RunEditAsync(Message msg, string prompt, CancellationToken сancellationToken)
    {
        var images = new List<byte[]>();

        if (msg.ReplyToMessage?.Photo != null)
        {
            var fileId = msg.ReplyToMessage.Photo.MaxBy(x => x.FileSize)!.FileId;
            images.Add(await _telegramBotClient.DownloadFileAsync(fileId, сancellationToken));
        }

        if (msg.Photo != null)
        {
            var fileId = msg.Photo.MaxBy(x => x.FileSize)!.FileId;
            images.Add(await _telegramBotClient.DownloadFileAsync(fileId, сancellationToken));
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

    private bool IsAdmin(Message msg) =>
        !string.IsNullOrEmpty(_adminUsername) &&
        string.Equals(msg.From?.Username, _adminUsername, StringComparison.OrdinalIgnoreCase);

    private bool TryConsumeFreeEdit(long userId)
    {
        var key = FreeEditKey(userId);
        if (_memoryCache.TryGetValue(key, out _))
        {
            return false;
        }

        _memoryCache.Set(key, true, FreeEditPeriod);
        return true;
    }

    private void ReleaseFreeEdit(long userId) => _memoryCache.Remove(FreeEditKey(userId));

    private static string FreeEditKey(long userId) => $"free_edit:{userId}";
}
