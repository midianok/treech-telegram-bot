using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using Saturn.Telegram.Db;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations.Ai;

public class SummaryGenerationOperation : OperationBase
{
    private readonly ChatClient _chatClient;
    private readonly IDbContextFactory<SaturnContext> _contextFactory;
    
    public SummaryGenerationOperation(IConfiguration configuration, IDbContextFactory<SaturnContext> contextFactory)
    {
        _contextFactory = contextFactory;
        _chatClient = new ChatClient("gpt-4o", configuration.GetSection("OPEN_AI_KEY").Value);
    }
    
    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var context = await _contextFactory.CreateDbContextAsync();
        var messages = await context.Messages
            .Where(x => x.ChatId == msg.Chat.Id && x.MessageDate > DateTime.Now.AddYears(-1) && !string.IsNullOrEmpty(x.Text))
            .Select(x => new
            {
                x.User!.Username,
                x.Text,
                x.MessageDate
            })
            .ToListAsync();

        var request =
            "Сделай саммари по следующим сообщениям из чата, ответь в формате Plain Text, полностью на русском языке. Структура: 1 строчка это одно сообщение в формате [дата_сообщения] пробел [имя_отправителя] пробел [сообщение]. Список сообщений в порядке их отправления:";

        var reqBuilder = new StringBuilder(request);

        foreach (var message in messages)
        {
            reqBuilder.AppendLine($"{message.MessageDate} {message.Username} {message.Text!.Replace("\n", "")}");
        }
        var clientResult = _chatClient.CompleteChatAsync(reqBuilder.ToString());

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

        await TelegramBotClient.SendMessage(msg.Chat, result, ParseMode.None, new ReplyParameters { MessageId = msg.Id } );
    }

    protected override bool ValidateOnMessage(Message msg, UpdateType type) => 
        !string.IsNullOrEmpty(msg.Text) &&
        msg.Text.StartsWith("саммари", StringComparison.CurrentCultureIgnoreCase);
}