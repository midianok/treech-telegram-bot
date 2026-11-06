using System.Net;

namespace Saturn.Telegram.Db.Entities;

public static class UserEntityExtensions
{
    public static string GetDisplayName(this UserEntity user)
    {
        if (!string.IsNullOrWhiteSpace(user.Username))
        {
            return $"@{user.Username}";
        }

        var fullName = string.Join(' ', new[] { user.FirstName, user.LastName }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        return string.IsNullOrWhiteSpace(fullName) ? user.Id.ToString() : fullName;
    }

    /// <summary>
    /// HTML-имя пользователя для таблиц лидеров (ParseMode.Html): кликабельное, но без упоминания.
    /// Ссылка https://t.me/username не является mention, поэтому уведомление не приходит
    /// (в отличие от @username и tg://user?id=). Без username — просто экранированный текст.
    /// </summary>
    public static string GetSilentHtmlLink(this UserEntity user) =>
        GetSilentHtmlLink(user.Id, user.Username, user.FirstName, user.LastName);

    public static string GetSilentHtmlLink(long userId, string? username, string? firstName, string? lastName)
    {
        var fullName = string.Join(' ', new[] { firstName, lastName }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        var name = !string.IsNullOrWhiteSpace(fullName)
            ? fullName
            : !string.IsNullOrWhiteSpace(username) ? username : userId.ToString();

        var encodedName = WebUtility.HtmlEncode(name);

        if (string.IsNullOrWhiteSpace(username))
        {
            return encodedName;
        }

        return $"<a href=\"https://t.me/{WebUtility.HtmlEncode(username)}\">{encodedName}</a>";
    }
}
