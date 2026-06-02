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
}
