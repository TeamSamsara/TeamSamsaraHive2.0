using System.Text.RegularExpressions;

namespace TeamSamsara.Hive.Core.Utilities;

// Post templates use two kinds of blanks:
//   {opponent}   filled in by the bot from the planned event (lowercase key)
//   [CAPTION]    written by the person posting; left as-is
public static class TemplateText
{
    private static readonly Regex PlaceholderPattern = new(@"\{(?<key>[a-z0-9_]+)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex KeyPattern = new(@"^[a-z][a-z0-9_]{0,31}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // A valid {key}: lowercase letter first, then letters, digits or _ (max 32)
    public static bool IsValidKey(string? key) => key != null && KeyPattern.IsMatch(key);

    // "Stream Link" -> "stream_link"; returns null if nothing usable is left
    public static string? ToKey(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var key = Regex.Replace(text.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
        if (key.Length > 32) key = key[..32].TrimEnd('_');
        if (key.Length > 0 && char.IsDigit(key[0])) key = "f_" + key;
        return IsValidKey(key) ? key : null;
    }

    // Every distinct {key} used in the text, in order of first use
    public static IReadOnlyList<string> FindPlaceholders(string? text)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<string>();
        return PlaceholderPattern.Matches(text)
            .Select(m => m.Groups["key"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    // Replaces each {key} that has a non-empty value. Keys without a value
    // stay in the text and are reported in missing, so the poster sees them.
    public static string Fill(string? text, IReadOnlyDictionary<string, string?> values, out IReadOnlyList<string> missing)
    {
        var notFilled = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            missing = notFilled;
            return string.Empty;
        }

        var result = PlaceholderPattern.Replace(text, m =>
        {
            var key = m.Groups["key"].Value;
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value!;
            }

            if (!notFilled.Contains(key)) notFilled.Add(key);
            return m.Value;
        });

        missing = notFilled;
        return result;
    }
}
