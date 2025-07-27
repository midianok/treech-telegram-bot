using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;

namespace Saturn.Telegram.Lib.Extensions;

public static class Extension
{
    public static string GetSectionOrThrow(this IConfiguration configuration, string key)
    {
        var item = configuration.GetSection(key).Value;
        if (string.IsNullOrWhiteSpace(item))
        {
            throw new Exception($"Configuration item {key} not presented");
        }

        return item;
    }
        
    public static TelegramBotClient Use(this TelegramBotClient telegramBotClient, IOperation operation) 
    {
        telegramBotClient.OnError += operation.OnErrorAsync;
        telegramBotClient.OnMessage += operation.OnMessageAsync;
        telegramBotClient.OnUpdate += operation.OnUpdateAsync;
        return telegramBotClient;
    }
}