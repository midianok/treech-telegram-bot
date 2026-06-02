using Saturn.Bot.Service.Extensions;
using Saturn.Bot.Service.Services.Abstractions;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Infrastructure;

public class PaymentOperation : IOperation
{
    private const string BalanceCommand = "баланс";

    private readonly ISparkShop _sparkShop;

    public PaymentOperation(ISparkShop sparkShop)
    {
        _sparkShop = sparkShop;
    }

    public bool Validate(Message msg, UpdateType type) =>
        msg.SuccessfulPayment != null || msg.HasText(BalanceCommand);

    public async Task OnMessageAsync(Message msg, UpdateType type, CancellationToken сancellationToken)
    {
        if (msg.SuccessfulPayment != null)
        {
            await _sparkShop.HandleSuccessfulPaymentAsync(msg, сancellationToken);
            return;
        }

        if (msg.From != null)
        {
            var balance = await _sparkShop.GetBalanceAsync(msg.From.Id, сancellationToken);
            await _sparkShop.SendOfferAsync(msg, $"Баланс: {balance} искр.", сancellationToken);
        }
    }

    public Task OnUpdateAsync(Update update) =>
        _sparkShop.TryHandleUpdateAsync(update, CancellationToken.None);
}
