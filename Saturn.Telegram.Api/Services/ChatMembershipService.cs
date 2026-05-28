using Microsoft.Extensions.Caching.Memory;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Api.Services;

public class ChatMembershipService(ITelegramBotClient botClient, IMemoryCache cache)
{
    public async Task<bool> IsMemberAsync(long chatId, long? userId, CancellationToken ct = default)
    {
        if (!userId.HasValue)
            return true;

        var key = $"chat_member:{chatId}:{userId.Value}";
        if (cache.TryGetValue(key, out bool cached))
            return cached;

        bool isMember;
        TimeSpan cacheDuration;
        try
        {
            var member = await botClient.GetChatMember(chatId, userId.Value, ct);
            isMember = member.Status is not ChatMemberStatus.Left and not ChatMemberStatus.Kicked;
            cacheDuration = TimeSpan.FromMinutes(5);
        }
        catch
        {
            isMember = false;
            cacheDuration = TimeSpan.FromSeconds(30);
        }

        cache.Set(key, isMember, cacheDuration);
        return isMember;
    }
}
