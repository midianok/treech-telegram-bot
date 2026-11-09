using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Saturn.Bot.Service.Options;
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

    private const string StarsCurrency = "XTR";
    private const string RubCurrency = "RUB";
    private const string PayloadPrefix = "sparks";
    private const string BuyCallbackPrefix = "buy:";
    private const string BuyRubCallbackPrefix = "buyrub:";

    // Top-up bundles (stars / rubles -> coins), see docs/monetisation-plan.md §3.
    // Rub is null for bundles below the YooKassa minimum: they're sold for stars only.
    private static readonly SparkBundle[] Bundles =
    [
        new(Stars: 12, Rub: null, Sparks: 12),
        new(Stars: 100, Rub: 99, Sparks: 100),
        new(Stars: 500, Rub: 449, Sparks: 550),
        new(Stars: 1000, Rub: 849, Sparks: 1150),
    ];

    private readonly ITelegramBotClient _bot;
    private readonly ICoinRepository _coinRepository;
    private readonly ILogger<SparkShop> _logger;
    private readonly string? _yookassaProviderToken;

    public SparkShop(ITelegramBotClient bot, ICoinRepository coinRepository, IOptions<BotOptions> botOptions, ILogger<SparkShop> logger)
    {
        _bot = bot;
        _coinRepository = coinRepository;
        _logger = logger;
        _yookassaProviderToken = string.IsNullOrWhiteSpace(botOptions.Value.YookassaProviderToken)
            ? null
            : botOptions.Value.YookassaProviderToken;
    }

    public Task<long> GetBalanceAsync(long userId, CancellationToken cancellationToken) =>
        _coinRepository.GetBalanceAsync(userId, cancellationToken);

    public async Task SendOfferAsync(Message msg, string reason, CancellationToken cancellationToken)
    {
        var rows = Bundles.Select(b =>
        {
            var row = new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData($"{b.Sparks} трич койнов — {b.Stars} ⭐", $"{BuyCallbackPrefix}{b.Stars}"),
            };
            if (_yookassaProviderToken != null && b.Rub != null)
            {
                row.Add(InlineKeyboardButton.WithCallbackData($"💳 {b.Rub} ₽", $"{BuyRubCallbackPrefix}{b.Stars}"));
            }

            return row;
        });

        await _bot.SendMessage(
            msg.Chat,
            $"{reason}\nОдно редактирование — {ImageEditCost} трич койнов. Пополни баланс:",
            replyParameters: new ReplyParameters { MessageId = msg.MessageId },
            replyMarkup: new InlineKeyboardMarkup(rows),
            cancellationToken: cancellationToken);
    }

    public async Task<bool> TryHandleUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        if (update.CallbackQuery is { Data: not null } callback)
        {
            if (callback.Data.StartsWith(BuyCallbackPrefix, StringComparison.Ordinal))
            {
                await HandleBuyCallbackAsync(callback, callback.Data[BuyCallbackPrefix.Length..], rub: false, cancellationToken);
                return true;
            }

            if (callback.Data.StartsWith(BuyRubCallbackPrefix, StringComparison.Ordinal))
            {
                await HandleBuyCallbackAsync(callback, callback.Data[BuyRubCallbackPrefix.Length..], rub: true, cancellationToken);
                return true;
            }
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

        _logger.LogInformation(
            "Coins top-up: user {UserId}, {Sparks} coins, {Amount} {Currency}, telegram charge {TelegramChargeId}, provider charge {ProviderChargeId}",
            msg.From.Id, sparks, payment.TotalAmount, payment.Currency, payment.TelegramPaymentChargeId, payment.ProviderPaymentChargeId);

        var balance = await _coinRepository.GetBalanceAsync(msg.From.Id, cancellationToken);
        await _bot.SendMessage(
            msg.Chat,
            $"Зачислено {sparks} трич койнов ✨ Баланс: {balance}.",
            replyParameters: new ReplyParameters { MessageId = msg.MessageId },
            cancellationToken: cancellationToken);
    }

    private async Task HandleBuyCallbackAsync(CallbackQuery callback, string bundleKey, bool rub, CancellationToken cancellationToken)
    {
        await _bot.AnswerCallbackQuery(callback.Id, cancellationToken: cancellationToken);

        if (callback.Message == null)
        {
            return;
        }

        var bundle = Bundles.FirstOrDefault(b => b.Stars.ToString() == bundleKey);
        if (bundle == null || (rub && (bundle.Rub == null || _yookassaProviderToken == null)))
        {
            return;
        }

        await _bot.SendInvoice(
            chatId: callback.Message.Chat,
            title: "Трич койны",
            description: $"{bundle.Sparks} трич койнов для редактирования изображений",
            payload: $"{PayloadPrefix}:{bundle.Stars}:{bundle.Sparks}",
            // Stars (XTR) require an empty provider token; RUB goes through YooKassa and is priced in kopecks.
            providerToken: rub ? _yookassaProviderToken : "",
            currency: rub ? RubCurrency : StarsCurrency,
            prices: [new LabeledPrice($"{bundle.Sparks} трич койнов", rub ? bundle.Rub!.Value * 100 : bundle.Stars)],
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

    private sealed record SparkBundle(int Stars, int? Rub, int Sparks);
}
