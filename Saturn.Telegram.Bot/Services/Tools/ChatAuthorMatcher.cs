using System.Text;
using Saturn.Telegram.Db.Repositories.Abstractions;

namespace Saturn.Bot.Service.Services.Tools;

/// <summary>
/// Finds chat members by a loosely written name: part of a username, first or last name,
/// in any keyboard layout and with small typos.
/// </summary>
public static class ChatAuthorMatcher
{
    public const int MaxCandidates = 10;

    private static readonly Dictionary<char, string> CyrillicToLatin = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "e", ['ж'] = "zh",
        ['з'] = "z", ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o",
        ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u", ['ф'] = "f", ['х'] = "h", ['ц'] = "ts",
        ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sch", ['ъ'] = "", ['ы'] = "y", ['ь'] = "", ['э'] = "e", ['ю'] = "yu",
        ['я'] = "ya"
    };

    public static List<ChatAuthor> Find(List<ChatAuthor> chatAuthors, string author)
    {
        var key = ToSearchKey(author);
        if (key.Length == 0)
        {
            return [];
        }

        var matches = chatAuthors
            .Where(a => new[] { a.Username, a.FirstName, a.LastName, $"{a.FirstName} {a.LastName}" }
                .Any(name => ToSearchKey(name).Contains(key)))
            .Take(MaxCandidates)
            .ToList();
        if (matches.Count > 0)
        {
            return matches;
        }

        // Nicknames and typos ("некита" for "Никита", "вова" vs "вава"): allow a couple of letters to differ
        var maxDistance = key.Length <= 3 ? 0 : key.Length <= 6 ? 1 : 2;
        if (maxDistance == 0)
        {
            return [];
        }

        return chatAuthors
            .Where(a => new[] { a.Username, a.FirstName, a.LastName }
                .Any(name => ContainsApproximately(ToSearchKey(name), key, maxDistance)))
            .Take(MaxCandidates)
            .ToList();
    }

    /// <summary>The candidate whose username is exactly <paramref name="author"/> (with or without @), if any.</summary>
    public static ChatAuthor? FindExactUsername(IEnumerable<ChatAuthor> candidates, string author)
    {
        var username = author.Trim().TrimStart('@');
        return candidates.FirstOrDefault(a => string.Equals(a.Username, username, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Brings a name to a comparable form: lowercase latin letters and digits only,
    /// so "N. T. G." and "нтг" both become "ntg".
    /// </summary>
    public static string ToSearchKey(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var c in value.ToLowerInvariant())
        {
            if (CyrillicToLatin.TryGetValue(c, out var latin))
            {
                builder.Append(latin);
            }
            else if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
        }
        return builder.ToString();
    }

    public static string FormatCandidate(ChatAuthor author) =>
        $"{$"{author.FirstName} {author.LastName}".Trim()} (@{author.Username}, id {author.UserId}, {author.MessageCount} msgs)";

    /// <summary>Whether some substring of <paramref name="value"/> is within <paramref name="maxDistance"/> edits of <paramref name="key"/>.</summary>
    private static bool ContainsApproximately(string value, string key, int maxDistance)
    {
        if (value.Length == 0)
        {
            return false;
        }

        // Sellers' approximate substring matching: the match may start anywhere in value, so row 0 costs nothing
        var previous = new int[value.Length + 1];
        var current = new int[value.Length + 1];
        for (var i = 1; i <= key.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= value.Length; j++)
            {
                var cost = key[i - 1] == value[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }

        return previous.Min() <= maxDistance;
    }
}
