using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Lib.Extensions;

internal static class UpdateTypeExtensions
{
    internal static bool IsMessage(this UpdateType update) => 
        update == UpdateType.Message;
}