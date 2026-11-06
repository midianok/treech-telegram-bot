using System.Text;

namespace Saturn.Telegram.Db.Entities;

public static class GameLeaderboardFormatter
{
    /// <summary>
    /// Строки таблицы лидеров (ParseMode.Html): место, имя без упоминания и счёт.
    /// </summary>
    public static string Format(IEnumerable<IGameScoreEntity> entries)
    {
        var builder = new StringBuilder();
        var position = 1;
        foreach (var entry in entries)
        {
            var userName = entry.User?.GetSilentHtmlLink() ?? entry.UserId.ToString();
            builder.Append($"{GetEmoji(position++)} {userName}: {entry.Score}\n");
        }

        return builder.ToString();
    }

    private static string GetEmoji(int position) =>
        position switch
        {
            1 => "🥇",
            2 => "🥈",
            3 => "🥉",
            _ => string.Empty
        };
}
