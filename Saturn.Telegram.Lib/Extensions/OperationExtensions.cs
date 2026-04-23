using Saturn.Telegram.Lib.Attributes;
using Saturn.Telegram.Lib.Operation;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Saturn.Telegram.Lib.Extensions;

public static class OperationExtensions
{
    extension(IOperation operation)
    {
        public T? GetAttribute<T>() where T : Attribute =>
            operation.GetType()
                .GetCustomAttributes(typeof(T), false)
                .OfType<T>()
                .FirstOrDefault();

        public bool IsIgnored() =>
            operation.GetAttribute<IgnoredAttribute>() != null;

        public bool IsAllowed(long? userId)
        {
            var attr = operation.GetAttribute<AllowAttribute>();
            return attr == null || attr.UserIds.Contains(userId ?? 0);
        }

        public async Task<bool> IsChatOnlyViolatedAsync(Message msg, TelegramBotClient botClient)
        {
            var attr = operation.GetAttribute<ChatOnlyAttribute>();
            if (attr == null || msg.Chat.Type is ChatType.Group or ChatType.Supergroup)
            {
                return false;
            }

            await botClient.SendMessage(msg.Chat, attr.Message, replyParameters: new ReplyParameters { MessageId = msg.Id });
            return true;
        }
    }
}