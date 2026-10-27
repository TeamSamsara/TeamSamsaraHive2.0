using System.Text.RegularExpressions;

namespace TeamSamsara.Hive.Bot.Protocols.Models;

// One input of a platform's post template (YouTube "Title", Twitter "Post")
public sealed class PlatformField
{
    // Discord modals accept at most 4000 characters per text box
    public const int ModalMaxLength = 4000;

    public required string Key { get; init; }
    public required string Label { get; init; }

    // The platform's own limit; going over is a warning, not an error,
    // because {placeholders} change the final length
    public int? Limit { get; init; }

    public bool Multiline { get; init; }
    public string? Placeholder { get; init; }

    // How the platform counts length (Twitter counts each link as 23)
    public Func<string, int> Measure { get; init; } = text => text.Length;

    // Modal label, e.g. "Title (100 max)". Discord labels stop at 45 characters.
    public string InputLabel
    {
        get
        {
            var label = Limit is { } limit ? $"{Label} ({limit} max)" : Label;
            return label.Length <= 45 ? label : label[..45];
        }
    }
}

public sealed class PlatformDefinition
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Emoji { get; init; }
    public required IReadOnlyList<PlatformField> Fields { get; init; }

    // Extra rules beyond length, e.g. Instagram's hashtag cap
    public Func<PostTemplate, IEnumerable<string>> ExtraChecks { get; init; } = _ => Array.Empty<string>();

    public PlatformField? FindField(string key) => Fields.FirstOrDefault(f => f.Key == key);
}

// The platforms a post template can target, with their limits.
// Limits as of late 2026; update here if a platform changes them.
public static class Platforms
{
    public const int MaxChecklistItems = 15;
    public const int MaxChecklistItemLength = 150;

    private static readonly Regex UrlPattern = new(@"https?://\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex HashtagPattern = new(@"(?<![\w&])#[\p{L}\p{N}_]+", RegexOptions.Compiled);

    public static readonly PlatformDefinition YouTube = new()
    {
        Key = "youtube",
        Name = "YouTube",
        Emoji = "▶️",
        Fields = new[]
        {
            new PlatformField { Key = "title", Label = "Title", Limit = 100, Placeholder = "{opponent} comms | [HOOK]" },
            // YouTube allows 5000, but a Discord text box stops at 4000
            new PlatformField { Key = "description", Label = "Description", Limit = 5000, Multiline = true, Placeholder = "Discord boxes hold 4000; add the rest when posting" },
            new PlatformField { Key = "tags", Label = "Tags, comma separated", Limit = 500, Multiline = true, Placeholder = "team samsara, valorant, voice comms" }
        }
    };

    public static readonly PlatformDefinition TikTok = new()
    {
        Key = "tiktok",
        Name = "TikTok",
        Emoji = "🎵",
        Fields = new[]
        {
            new PlatformField { Key = "caption", Label = "Caption", Limit = 4000, Multiline = true, Placeholder = "Only the first ~80 characters show in the feed" },
            new PlatformField { Key = "onscreen", Label = "On-screen text", Limit = 300, Multiline = true }
        }
    };

    public static readonly PlatformDefinition Instagram = new()
    {
        Key = "instagram",
        Name = "Instagram",
        Emoji = "📸",
        Fields = new[]
        {
            new PlatformField { Key = "caption", Label = "Caption", Limit = 2200, Multiline = true, Placeholder = "Instagram allows at most 5 hashtags" },
            new PlatformField { Key = "alt", Label = "Alt text", Limit = 100, Multiline = true }
        },
        ExtraChecks = post =>
        {
            var count = HashtagPattern.Matches(post.Fields.GetValueOrDefault("caption") ?? string.Empty).Count;
            return count > 5
                ? new[] { $"Instagram allows 5 hashtags per post; this caption has {count}." }
                : Array.Empty<string>();
        }
    };

    public static readonly PlatformDefinition Twitter = new()
    {
        Key = "twitter",
        Name = "Twitter / X",
        Emoji = "🐦",
        Fields = new[]
        {
            new PlatformField { Key = "post", Label = "Post", Limit = 280, Multiline = true, Measure = MeasureTweet }
        }
    };

    public static readonly PlatformDefinition Discord = new()
    {
        Key = "discord",
        Name = "Discord",
        Emoji = "💬",
        Fields = new[]
        {
            new PlatformField { Key = "message", Label = "Message", Limit = 2000, Multiline = true },
            new PlatformField { Key = "channel", Label = "Channel to post in", Limit = 100, Placeholder = "#announcements" }
        }
    };

    public static IReadOnlyList<PlatformDefinition> All { get; } = new[] { YouTube, TikTok, Instagram, Twitter, Discord };

    public static PlatformDefinition? Find(string? key) =>
        All.FirstOrDefault(p => string.Equals(p.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));

    // Twitter counts every link as 23 characters, whatever its real length
    public static int MeasureTweet(string text) =>
        UrlPattern.Replace(text, new string('x', 23)).Length;

    // Problems that should block saving (unknown fields, too long for Discord)
    public static IEnumerable<string> FindErrors(PlatformDefinition platform, PostTemplate post)
    {
        foreach (var key in post.Fields.Keys.Where(k => platform.FindField(k) == null))
        {
            yield return $"{platform.Name} has no field called `{key}` (use: {string.Join(", ", platform.Fields.Select(f => f.Key))}).";
        }

        foreach (var field in platform.Fields)
        {
            var text = post.Fields.GetValueOrDefault(field.Key);
            if (text != null && text.Length > PlatformField.ModalMaxLength)
            {
                yield return $"{platform.Name} {field.Label} is {text.Length} characters; the most the bot stores is {PlatformField.ModalMaxLength}.";
            }
        }

        if (post.Checklist.Count > MaxChecklistItems)
        {
            yield return $"{platform.Name} checklist has {post.Checklist.Count} items; the most is {MaxChecklistItems}.";
        }

        foreach (var item in post.Checklist.Where(i => i.Length > MaxChecklistItemLength))
        {
            yield return $"{platform.Name} checklist item is over {MaxChecklistItemLength} characters: \"{item[..40]}…\"";
        }
    }

    // Things worth knowing that don't block saving (over the platform's limit)
    public static IEnumerable<string> FindWarnings(PlatformDefinition platform, PostTemplate post)
    {
        foreach (var field in platform.Fields)
        {
            var text = post.Fields.GetValueOrDefault(field.Key);
            if (string.IsNullOrEmpty(text) || field.Limit == null) continue;

            var length = field.Measure(text);
            if (length > field.Limit)
            {
                yield return $"{platform.Name} {field.Label} is {length}/{field.Limit} characters (before filling in {{placeholders}}).";
            }
        }

        foreach (var warning in platform.ExtraChecks(post))
        {
            yield return warning;
        }
    }
}
