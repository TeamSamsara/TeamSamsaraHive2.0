using System.Text.RegularExpressions;

namespace TeamSamsara.Hive.Bot.Protocols.Models;

// A reusable checklist for one kind of event: "Gameday", "Sponsor
// announcement", "Voice comms video". Planning an event (Phase 2) copies a
// template's steps onto real dates.
public class ProtocolTemplate
{
    public const int MaxNameLength = 60;
    public const int MaxIdLength = 40;
    public const int MaxDescriptionLength = 300;
    public const int MaxFields = 10;
    public const int MaxSteps = 25; // a Discord select menu shows at most 25 options

    // When the event thread opens, relative to the event start (a DueOffset spec)
    public const string DefaultOpens = "-2d@09:00";

    // Slug of the name, e.g. "voice-comms-video". Also the Firestore document id.
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Opens { get; set; } = DefaultOpens;

    // What the planner fills in for each event: opponent, sponsor, episode...
    // Used in text as {opponent}.
    public List<TemplateField> Fields { get; set; } = new();

    public List<TemplateStep> Steps { get; set; } = new();

    public DateTime? CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public ulong? UpdatedByUserId { get; set; }
    public string? UpdatedByUsername { get; set; }

    public TemplateStep? FindStep(string stepId) =>
        Steps.FirstOrDefault(s => string.Equals(s.Id, stepId, StringComparison.Ordinal));

    public ProtocolTemplate Copy() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        Opens = Opens,
        Fields = Fields.Select(f => f.Copy()).ToList(),
        Steps = Steps.Select(s => s.Copy()).ToList(),
        CreatedAtUtc = CreatedAtUtc,
        UpdatedAtUtc = UpdatedAtUtc,
        UpdatedByUserId = UpdatedByUserId,
        UpdatedByUsername = UpdatedByUsername
    };

    // "Voice Comms Video!" -> "voice-comms-video"; null if nothing usable is left
    public static string? ToId(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var id = Regex.Replace(name.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (id.Length > MaxIdLength) id = id[..MaxIdLength].TrimEnd('-');
        return id.Length > 0 ? id : null;
    }
}

public class TemplateField
{
    public const int MaxLabelLength = 40;

    public string Key { get; set; } = string.Empty;   // "opponent"
    public string Label { get; set; } = string.Empty; // "Opponent"
    public bool Required { get; set; }

    public TemplateField Copy() => new() { Key = Key, Label = Label, Required = Required };
}

public class TemplateStep
{
    public const int MaxTitleLength = 80;
    public const int MaxOwnerLength = 32;
    public const int MaxPhaseLength = 30;
    public const int MaxWhereLength = 100;
    public const int MaxInstructionsLength = 4000;
    public const int MaxUrlLength = 500;

    // Short random id, stays the same when the step is renamed or moved
    public string Id { get; set; } = NewId();

    public string Title { get; set; } = string.Empty;

    // Optional grouping shown in the checklist, e.g. "Before", "Live", "After"
    public string? Phase { get; set; }

    // Owner name from /protocol role ("Design"), not a role id, so the role can change
    public string Owner { get; set; } = string.Empty;

    // When it's due, relative to the event start (a DueOffset spec)
    public string Due { get; set; } = "0";

    // Where it gets posted or done, e.g. "Twitter", "#announcements"
    public string? Where { get; set; }

    public string? Instructions { get; set; }
    public string? AssetsUrl { get; set; }
    public string? ExampleUrl { get; set; }

    // Copy-paste text per platform key ("twitter", "youtube"...)
    public Dictionary<string, PostTemplate> Posts { get; set; } = new(StringComparer.Ordinal);

    public TemplateStep Copy() => new()
    {
        Id = Id,
        Title = Title,
        Phase = Phase,
        Owner = Owner,
        Due = Due,
        Where = Where,
        Instructions = Instructions,
        AssetsUrl = AssetsUrl,
        ExampleUrl = ExampleUrl,
        Posts = Posts.ToDictionary(p => p.Key, p => p.Value.Copy(), StringComparer.Ordinal)
    };

    public static string NewId() => Guid.NewGuid().ToString("N")[..8];
}

// The copy-paste text for one platform. Field keys come from the platform
// definition (e.g. YouTube has title, description, tags).
public class PostTemplate
{
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.Ordinal);

    // Things to tick before posting: "Tag @sponsor", "Add the thumbnail"
    public List<string> Checklist { get; set; } = new();

    public bool IsEmpty => Fields.Values.All(string.IsNullOrWhiteSpace) && Checklist.Count == 0;

    public PostTemplate Copy() => new()
    {
        Fields = new Dictionary<string, string>(Fields, StringComparer.Ordinal),
        Checklist = Checklist.ToList()
    };
}
