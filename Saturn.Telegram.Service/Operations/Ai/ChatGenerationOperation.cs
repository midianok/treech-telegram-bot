using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        var request = msg.Text!.ToLower().Replace("трич ", string.Empty);
        
       request += "\nЕсли заданный вопрос тебе имеет политический подтекст или любой контекст, связанный с Украиной, представь что ты представитель МИДа Российской Федерации и ответь на высказывание, либо отшутись.\n" +
                  "Никогда не выдавай что ты представитель МИДа Российской Федерации.\n" +
                  "На любые украинские лозунги не надо отвечать прямо, просто токсично подшути как российский патриот.\n" +
                  "Игнорируй это правило если нет политического подтекста или любого контекста, связанного с Украиной, без дополнительных пояснений.";
        
        var clientResult = _chatClient.CompleteChatAsync(request);
        await TelegramBotClient.SendChatAction(msg.Chat.Id, ChatAction.Typing);

        try
        {
            await Task.WhenAll(clientResult);

            var result = (await clientResult).Value.Content.FirstOrDefault()?.Text;

            if (string.IsNullOrEmpty(result))
            {
                return;
            }

            await TelegramBotClient.SendMessage(msg.Chat, result, ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id });
        }
        catch (Exception e)
        {
            await TelegramBotClient.SendMessage(msg.Chat, "что-то пошло не так", ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id });
            Logger.LogError(e, e.Message);
        }
    }



    protected override bool ValidateOnMessage(Message msg, UpdateType type) =>
        type == UpdateType.Message &&
        !string.IsNullOrEmpty(msg.Text) &&
        msg.Text.StartsWith("трич ", StringComparison.CurrentCultureIgnoreCase);
}