using Microsoft.Extensions.Caching.Memory;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Api.Services;

public class ChatMembershipService(
    ITelegramBotClient botClient,
    IMemoryCache cache,
    ILogger<ChatMembershipService> logger)
{
    public async Task<bool> IsMemberAsync(long chatId, long? userId, CancellationToken сancellationToken)
    {
        if (!userId.HasValue)
            return true;

        // Личный чат с ботом: chatId совпадает с userId, проверять членство не нужно.
        if (chatId == userId.Value)
            return true;

        var key = $"chat_member:{chatId}:{userId.Value}";
        if (cache.TryGetValue(key, out bool cached))
            return cached;

        try
        {
            var member = await botClient.GetChatMember(chatId, userId.Value, сancellationToken);
            var isMember = member.Status is not ChatMemberStatus.Left and not ChatMemberStatus.Kicked;
            cache.Set(key, isMember, TimeSpan.FromMinutes(5));
            return isMember;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Сбой Telegram API не кешируем: иначе любые запросы игрока (в том числе отправка рекорда)
            // отклоняются, пока не протухнет кеш.
            logger.LogWarning(ex, "GetChatMember failed for chat {ChatId}, user {UserId}", chatId, userId.Value);
            return false;
        }
    }
}
