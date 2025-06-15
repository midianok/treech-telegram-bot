using System.Globalization;
using Humanizer;
using Saturn.Telegram.Lib.Attributes;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Bot.Service.Operations;

public class LinaWorkTimeOperation : OperationBase
{
    protected override async Task ProcessOnMessageAsync(Message msg, UpdateType type)
    {
        var workStartTime = new TimeSpan(0, 4, 30, 00);
        var workEndTime = TimeSpan.FromHours(13);
        var now = DateTime.UtcNow.TimeOfDay;
        if (now < workStartTime)
        {
            await TelegramBotClient.SendMessage(msg.Chat, "работа ещё не началась", ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id } );
            return;
        }
        
        if (now > workEndTime)
        {
            await TelegramBotClient.SendMessage(msg.Chat, "работа уже кончилась", ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id } );
            return;
        }
        
        
        var elapsedString = (workEndTime - now).Humanize( precision: 2, culture: new CultureInfo("ru-RU"), collectionSeparator: " ").Replace(",", "");
       
        await TelegramBotClient.SendMessage(msg.Chat, $"через {elapsedString}", ParseMode.Markdown, new ReplyParameters { MessageId = msg.Id } );
    }

    protected override bool ValidateOnTextMessage(Message msg, UpdateType type) =>
        type == UpdateType.Message && 
        !string.IsNullOrEmpty(msg.Text) &&
        msg.Text.StartsWith("домой", StringComparison.CurrentCultureIgnoreCase);
}