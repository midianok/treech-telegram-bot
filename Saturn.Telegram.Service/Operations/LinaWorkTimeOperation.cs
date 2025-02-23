using System.Globalization;
using Humanizer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Saturn.Bot.Service.Operations.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations;

public class LinaWorkTimeOperation : OperationBase
{
    private readonly TelegramBotClient _telegramBotClient;
    
    public LinaWorkTimeOperation(ILogger<IOperation> logger, IConfiguration configuration, TelegramBotClient telegramBotClient) : base(logger, configuration)
    {
        _telegramBotClient = telegramBotClient;
    }

    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var workStartTime = new TimeSpan(0, 4, 30, 00);
        var workEndTime = TimeSpan.FromHours(13);
        var now = DateTime.UtcNow.TimeOfDay;
        if (now < workStartTime)
        {
            await _telegramBotClient.SendMessage(msg.Chat, "работа ещё не началась", ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id } );
            return;
        }
        
        if (now > workEndTime)
        {
            await _telegramBotClient.SendMessage(msg.Chat, "работа уже кончилась", ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id } );
            return;
        }
        
        
        var elapsedString = (workEndTime - now).Humanize( precision: 2, culture: new CultureInfo("ru-RU"), collectionSeparator: " ").Replace(",", "");
       
        await _telegramBotClient.SendMessage(msg.Chat, $"через {elapsedString}", ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id } );
    }

    protected override bool ValidateOnMessage(Message msg, UpdateType type) =>
        type == UpdateType.Message && 
        msg.From?.Id is 198607451 or 1691705486 &&
        !string.IsNullOrEmpty(msg.Text) &&
        msg.Text.StartsWith("домой", StringComparison.CurrentCultureIgnoreCase);
}