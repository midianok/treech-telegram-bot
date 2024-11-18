using Saturn.Bot.Service.Operations;
using Saturn.Bot.Service.Operations.Abstractions;
using Telegram.Bot;

namespace Saturn.Bot.Service.Extension;

public static class TelegramBotClientExtension
{
    public static TelegramBotClient AddOp(this TelegramBotClient botClient, IOperation operation)
    {
        botClient.OnError += operation.OnErrorAsync;
        botClient.OnMessage += operation.OnMessageAsync;
        botClient.OnUpdate += operation.OnUpdateAsync;
        return botClient;
    }
}