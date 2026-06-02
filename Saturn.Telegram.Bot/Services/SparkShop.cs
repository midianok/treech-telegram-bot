using Microsoft.Extensions.Logging;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Db.Repositories.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Payments;
using Telegram.Bot.Types.ReplyMarkups;

namespace Saturn.Bot.Service.Services;

public class SparkShop : ISparkShop
{
    public int ImageEditCost => 12;

    private const string Currency = "XTR";
    private const string PayloadPrefix = "sparks";
    private const string BuyCallbackPrefix = "buy:";

    // Telegram Stars top-up bundles (stars -> coins), see docs/monetisation-plan.md §3.
    private static readonly SparkBundle[] Bundles =
    [
        new(Stars: 12, Sparks: 12),
        new(Stars: 100, Sparks: 100),
        new(Stars: 500, Sparks: 550),
        new(Stars: 1000, Sparks: 1150),
    ];

    private readonly ITelegramBotClient _bot;
    private readonly ICoinRepository _coinRepository;
    private readonly ILogger<SparkShop> _logger;

    public SparkShop(ITelegramBotClient bot, ICoinRepository coinRepository, ILogger<SparkShop> logger)
    {
        _bot = bot;
        _coinRepository = coinRepository;
        _logger = logger;
    }

    public Task<long> GetBalanceAsync(long userId, CancellationToken cancellationToken) =>
        _coinRepository.GetBalanceAsync(userId, cancellationToken);

    public async Task SendOfferAsync(Message msg, string reason, CancellationToken cancellationToken)
    {
        var rows = Bundles
            .Select(b => new[]
            {
                InlineKeyboardButton.WithCallbackData($"{b.Sparks} искр — {b.Stars} ⭐", $"{BuyCallbackPrefix}{b.Stars}"),
            });

        await _bot.SendMessage(
            msg.Chat,
            $"{reason}\nОдно редактирование — {ImageEditCost} искр. Пополни баланс:",
            replyParameters: new ReplyParameters { MessageId = msg.MessageId },
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: cancellationToken);
    }

    public async Task<bool> TryHandleUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        if (update.CallbackQuery is { Data: not null } callback &&
            callback.Data.StartsWith(BuyCallbackPrefix, StringComparison.Ordinal))
        {
            await HandleBuyCallbackAsync(callback, cancellationToken);
            return true;
        }

        if (update.PreCheckoutQuery is { } preCheckout)
        {
            await _bot.AnswerPreCheckoutQuery(preCheckout.Id, cancellationToken: cancellationToken);
            return true;
        }

        return false;
    }

    public async Task HandleSuccessfulPaymentAsync(Message msg, CancellationToken cancellationToken)
    {
        var payment = msg.SuccessfulPayment;
        if (payment == null || msg.From == null)
        {
            return;
        }

        var sparks = ParseSparks(payment.InvoicePayload);
        if (sparks <= 0)
        {
            _logger.LogError("Unrecognized payment payload: {Payload}", payment.InvoicePayload);
            return;
        }

        var credited = await _coinRepository.CreditFromPaymentAsync(
            msg.From.Id, sparks, payment.TelegramPaymentChargeId, cancellationToken);
        if (!credited)
        {
            return; // duplicate delivery, already processed
        }

        var balance = await _coinRepository.GetBalanceAsync(msg.From.Id, cancellationToken);
        await _bot.SendMessage(
            msg.Chat,
            $"Зачислено {sparks} искр ✨ Баланс: {balance}.",
            replyParameters: new ReplyParameters { MessageId = msg.MessageId },
            cancellationToken: cancellationToken);
    }

    private async Task HandleBuyCallbackAsync(CallbackQuery callback, CancellationToken cancellationToken)
    {
        await _bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);

        if (callback.Message == null)
        {
            return;
        }

        var starsText = callback.Data![BuyCallbackPrefix.Length..];
        var bundle = Bundles.FirstOrDefault(b => b.Stars.ToString() == starsText);
        if (bundle == null)
        {
            return;
        }

        await _bot.SendInvoice(
            chatId: callback.Message.Chat,
            title: "Искры",
            description: $"{bundle.Sparks} искр для редактирования изображений",
            payload: $"{PayloadPrefix}:{bundle.Stars}:{bundle.Sparks}",
            providerToken: "", // empty for Telegram Stars (XTR)
            currency: Currency,
            prices: [new LabeledPrice($"{bundle.Sparks} искр", bundle.Stars)],
            cancellationToken: cancellationToken);
    }

    private static int ParseSparks(string? payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return 0;
        }

        var parts = payload.Split(':');
        return parts.Length == 3 && parts[0] == PayloadPrefix && int.TryParse(parts[2], out var sparks)
            ? sparks
            : 0;
    }

    private sealed record SparkBundle(int Stars, int Sparks);
}
