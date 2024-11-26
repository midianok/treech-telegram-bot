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

    public ChatGenerationOperation(ILogger<IOperation> logger, IConfiguration configuration, TelegramBotClient telegramBotClient) : base(logger, configuration)
    {
        _chatClient = new ChatClient(model: "gpt-4o-mini", apiKey: configuration.GetSection("OPEN_AI_KEY").Value);
        _telegramBotClient = telegramBotClient;
    }

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
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