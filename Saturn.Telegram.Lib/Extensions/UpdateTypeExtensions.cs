using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Lib.Extensions;

public static class UpdateTypeExtensions
{
    public static bool IsMessage(this UpdateType update) => 
        update == UpdateType.Message;
}