using System.Globalization;
using Humanizer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Saturn.Bot.Service.Operations.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations;

public class ChatGenerationOperation : OperationBase
{
    private readonly TelegramBotClient _telegramBotClient;
    private readonly ChatClient _chatClient;
    private readonly IMemoryCache _memoryCache;

    public ChatGenerationOperation(ILogger<IOperation> logger, IConfiguration configuration, TelegramBotClient telegramBotClient, IMemoryCache memoryCache) : base(logger, configuration)
    {
        _chatClient = new ChatClient(model: "gpt-4o-mini", apiKey: configuration.GetSection("OPEN_AI_KEY").Value);
        _telegramBotClient = telegramBotClient;
        _memoryCache = memoryCache;
    }

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var user = await _telegramBotClient.GetChatMember(msg.Chat.Id, msg.From!.Id);
        if (_memoryCache.TryGetValue(msg.From!.Id, out DateTime cooldownTime) && user.Status != ChatMemberStatus.Administrator)
        {
            var elapsed = (cooldownTime - DateTime.Now).Humanize(2, culture: new CultureInfo("ru-RU"), collectionSeparator: " ");
            await _telegramBotClient.SendMessage(msg.Chat.Id, $"Отдохни ещё {elapsed}", replyParameters: new ReplyParameters { MessageId = msg.MessageId } );
            return;
        }
        _memoryCache.Set(msg.From.Id, DateTime.Now.AddMinutes(2), TimeSpan.FromMinutes(2));
        
        var request = msg.Text!.ToLower().Replace("трич ", string.Empty);
        var clientResult = _chatClient.CompleteChatAsync(request);

        while (!clientResult.IsCompleted)
        {
            await _telegramBotClient.SendChatAction(msg.Chat.Id, ChatAction.Typing);
        }

        await Task.WhenAll(clientResult);

        var result = clientResult.Result.Value.Content.FirstOrDefault()?.Text;

        if (string.IsNullOrEmpty(result))
        {
            return;
        }

        await _telegramBotClient.SendMessage(msg.Chat, result, ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id } );
    }

    protected override bool ValidateOnMessage(Message msg, UpdateType type) =>
        type == UpdateType.Message &&
        !string.IsNullOrEmpty(msg.Text) &&
        msg.Text.StartsWith("трич ", StringComparison.CurrentCultureIgnoreCase);
}