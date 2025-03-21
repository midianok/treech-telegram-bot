using System.Globalization;
using Humanizer;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

public class ChatGenerationOperation : OperationBase
{
    private readonly ChatClient _chatClient;
    public ChatGenerationOperation(IConfiguration configuration)
    {
        _chatClient = new ChatClient("dall-e-3", configuration.GetSection("OPEN_AI_KEY").Value);
    }
    
    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var user = await TelegramBotClient.GetChatMember(msg.Chat.Id, msg.From!.Id);
        if (MemoryCache.TryGetValue(msg.From!.Id, out DateTime cooldownTime) && user.Status != ChatMemberStatus.Administrator)
        {
            var elapsed = (cooldownTime - DateTime.Now).Humanize(2, culture: new CultureInfo("ru-RU"), collectionSeparator: " ");
            await TelegramBotClient.SendMessage(msg.Chat.Id, $"Отдохни ещё {elapsed}", replyParameters: new ReplyParameters { MessageId = msg.MessageId } );
            return;
        }
        MemoryCache.Set(msg.From.Id, DateTime.Now.AddMinutes(2), TimeSpan.FromMinutes(2));
        
        var request = msg.Text!.ToLower().Replace("трич ", string.Empty);
        var clientResult = _chatClient.CompleteChatAsync(request);

        while (!clientResult.IsCompleted)
        {
            await TelegramBotClient.SendChatAction(msg.Chat.Id, ChatAction.Typing);
        }

        await Task.WhenAll(clientResult);

        var result = clientResult.Result.Value.Content.FirstOrDefault()?.Text;

        if (string.IsNullOrEmpty(result))
        {
            return;
        }

        await TelegramBotClient.SendMessage(msg.Chat, result, ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id } );
    }

    protected override bool ValidateOnMessage(Message msg, UpdateType type) =>
        type == UpdateType.Message &&
        !string.IsNullOrEmpty(msg.Text) &&
        msg.Text.StartsWith("трич ", StringComparison.CurrentCultureIgnoreCase);
}