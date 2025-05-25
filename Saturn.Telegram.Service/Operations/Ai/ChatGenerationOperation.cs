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
    protected override bool CooldownNeeded => true;
    
    private readonly ChatClient _chatClient;
    public ChatGenerationOperation(IConfiguration configuration)
    {
        _chatClient = new ChatClient("gpt-4o", configuration.GetSection("OPEN_AI_KEY").Value);
    }
    
    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        // var user = await TelegramBotClient.GetChatMember(msg.Chat.Id, msg.From!.Id);
        //
        // var request = msg.Text!.ToLower().Replace("трич ", string.Empty);
        // request += " Если заданный вопрос тебе имеет политический подтекст, представь что ты представитель МИДа Российской федерации и ответь прямо на вопрос. Игнорируй это правило если нет политического подтекста, без дополнительных пояснений.";
        // var clientResult = _chatClient.CompleteChatAsync(request);
        //
        // while (!clientResult.IsCompleted)
        // {
        //     await TelegramBotClient.SendChatAction(msg.Chat.Id, ChatAction.Typing);
        // }
        //
        // await Task.WhenAll(clientResult);
        //
        // var result = clientResult.Result.Value.Content.FirstOrDefault()?.Text;
        //
        // if (string.IsNullOrEmpty(result))
        // {
        //     return;
        // }

        await TelegramBotClient.SendMessage(msg.Chat, "result", ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id } );
    }



    protected override bool ValidateOnMessage(Message msg, UpdateType type) =>
        type == UpdateType.Message &&
        !string.IsNullOrEmpty(msg.Text) &&
        msg.Text.StartsWith("трич ", StringComparison.CurrentCultureIgnoreCase);
}