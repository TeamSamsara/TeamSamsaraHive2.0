using TeamSamsara.Hive.Bot.Protocols.Models;
using TeamSamsara.Hive.Core.Scheduling;
using TeamSamsara.Hive.Core.Utilities;

namespace TeamSamsara.Hive.Bot.Protocols.Templates;

public sealed class TemplateCheck
{
    // Must be fixed before saving
    public List<string> Errors { get; } = new();

    // Saved anyway, but shown so someone can fix them
    public List<string> Warnings { get; } = new();

    public bool IsValid => Errors.Count == 0;
}

// Validation and text helpers shared by the commands, the editor and import.
public static class TemplateRules
{
    // Filled in by the bot for every event, so templates can always use them
    public static readonly IReadOnlyDictionary<string, string> BuiltInPlaceholders = new Dictionary<string, string>
    {
        ["event"] = "the event's title",
        ["date"] = "the event date, e.g. Dec 14",
        ["time"] = "the start time in the server time zone, e.g. 7:00 PM ET",
        ["weekday"] = "the day, e.g. Monday"
    };

    #region Whole template

    // ownerNames: names from /protocol role. Unknown owners are a warning,
    // because the role can be mapped after the template is written.
    public static TemplateCheck Check(ProtocolTemplate t, IReadOnlyCollection<string> ownerNames)
    {
        var check = new TemplateCheck();

        if (string.IsNullOrWhiteSpace(t.Name) || t.Name.Length > ProtocolTemplate.MaxNameLength)
            check.Errors.Add($"The name must be 1–{ProtocolTemplate.MaxNameLength} characters.");
        if (ProtocolTemplate.ToId(t.Name) == null)
            check.Errors.Add("The name needs at least one letter or number.");
        if (t.Description?.Length > ProtocolTemplate.MaxDescriptionLength)
            check.Errors.Add($"The description is over {ProtocolTemplate.MaxDescriptionLength} characters.");
        if (!DueOffset.TryParse(t.Opens, out _, out var opensError))
            check.Errors.Add($"Thread opens: {opensError}");

        CheckFields(t.Fields, check);

        if (t.Steps.Count > ProtocolTemplate.MaxSteps)
            check.Errors.Add($"A template can have at most {ProtocolTemplate.MaxSteps} steps (this one has {t.Steps.Count}). Split it into two templates.");

        var stepIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < t.Steps.Count; i++)
        {
            var step = t.Steps[i];
            if (!stepIds.Add(step.Id))
                check.Errors.Add($"Step {i + 1}: duplicate step id `{step.Id}`.");
            CheckStep(step, $"Step {i + 1} ({Short(step.Title)})", ownerNames, check);
        }

        // {placeholders} that nothing will fill
        var known = new HashSet<string>(t.Fields.Select(f => f.Key).Concat(BuiltInPlaceholders.Keys), StringComparer.Ordinal);
        foreach (var step in t.Steps)
        {
            var unknown = StepTexts(step).SelectMany(TemplateText.FindPlaceholders).Distinct().Where(k => !known.Contains(k)).ToList();
            if (unknown.Count > 0)
            {
                check.Warnings.Add($"{Short(step.Title)} uses {string.Join(", ", unknown.Select(k => $"`{{{k}}}`"))}, which isn't a template field. Add it to the fields or it stays as-is.");
            }
        }

        // Steps due before the thread opens get announced late
        if (DueOffset.TryParse(t.Opens, out var opens, out _))
        {
            var reference = new DateTime(2030, 6, 12, 19, 0, 0, DateTimeKind.Utc);
            var opensAt = opens.ResolveUtc(reference, TimeZoneInfo.Utc);
            foreach (var step in t.Steps)
            {
                if (DueOffset.TryParse(step.Due, out var due, out _) && due.ResolveUtc(reference, TimeZoneInfo.Utc) < opensAt)
                {
                    check.Warnings.Add($"{Short(step.Title)} is due {due.Describe()}, before the thread opens ({opens.Describe()}).");
                }
            }
        }

        return check;
    }

    public static void CheckStep(TemplateStep step, string label, IReadOnlyCollection<string> ownerNames, TemplateCheck check)
    {
        if (string.IsNullOrWhiteSpace(step.Title) || step.Title.Length > TemplateStep.MaxTitleLength)
            check.Errors.Add($"{label}: the title must be 1–{TemplateStep.MaxTitleLength} characters.");

        if (string.IsNullOrWhiteSpace(step.Owner))
            check.Errors.Add($"{label}: an owner is required (a name from /protocol role list).");
        else if (step.Owner.Length > TemplateStep.MaxOwnerLength)
            check.Errors.Add($"{label}: the owner name is over {TemplateStep.MaxOwnerLength} characters.");
        else if (!ownerNames.Contains(step.Owner, StringComparer.OrdinalIgnoreCase))
            check.Warnings.Add($"{label}: owner **{step.Owner}** isn't mapped to a role yet. Use `/protocol role set name:{step.Owner}`.");

        if (!DueOffset.TryParse(step.Due, out _, out var dueError))
            check.Errors.Add($"{label}: due time: {dueError}");

        if (step.Phase?.Length > TemplateStep.MaxPhaseLength)
            check.Errors.Add($"{label}: the phase is over {TemplateStep.MaxPhaseLength} characters.");
        if (step.Where?.Length > TemplateStep.MaxWhereLength)
            check.Errors.Add($"{label}: \"where\" is over {TemplateStep.MaxWhereLength} characters.");
        if (step.Instructions?.Length > TemplateStep.MaxInstructionsLength)
            check.Errors.Add($"{label}: the instructions are over {TemplateStep.MaxInstructionsLength} characters.");

        foreach (var (name, url) in new[] { ("assets link", step.AssetsUrl), ("example link", step.ExampleUrl) })
        {
            if (!string.IsNullOrWhiteSpace(url) && !IsWebLink(url))
                check.Errors.Add($"{label}: the {name} must start with http:// or https:// and be under {TemplateStep.MaxUrlLength} characters.");
        }

        foreach (var (platformKey, post) in step.Posts)
        {
            var platform = Platforms.Find(platformKey);
            if (platform == null)
            {
                check.Errors.Add($"{label}: unknown platform `{platformKey}` (use: {string.Join(", ", Platforms.All.Select(p => p.Key))}).");
                continue;
            }

            check.Errors.AddRange(Platforms.FindErrors(platform, post).Select(e => $"{label}: {e}"));
            check.Warnings.AddRange(Platforms.FindWarnings(platform, post).Select(w => $"{label}: {w}"));
        }
    }

    // Field problems only, for the details form
    public static List<string> FieldErrors(List<TemplateField> fields)
    {
        var check = new TemplateCheck();
        CheckFields(fields, check);
        return check.Errors;
    }

    private static void CheckFields(List<TemplateField> fields, TemplateCheck check)
    {
        if (fields.Count > ProtocolTemplate.MaxFields)
            check.Errors.Add($"A template can ask for at most {ProtocolTemplate.MaxFields} fields.");

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (!TemplateText.IsValidKey(field.Key))
                check.Errors.Add($"Field `{field.Key}`: keys use lowercase letters, numbers and _ and start with a letter.");
            else if (BuiltInPlaceholders.ContainsKey(field.Key))
                check.Errors.Add($"Field `{field.Key}` is filled in automatically; pick another name.");
            else if (!keys.Add(field.Key))
                check.Errors.Add($"Field `{field.Key}` is listed twice.");

            if (string.IsNullOrWhiteSpace(field.Label) || field.Label.Length > TemplateField.MaxLabelLength)
                check.Errors.Add($"Field `{field.Key}`: the label must be 1–{TemplateField.MaxLabelLength} characters.");
        }
    }

    // Every text in a step that can contain {placeholders}
    public static IEnumerable<string> StepTexts(TemplateStep step)
    {
        if (step.Title != null) yield return step.Title;
        if (step.Instructions != null) yield return step.Instructions;
        if (step.Where != null) yield return step.Where;
        foreach (var post in step.Posts.Values)
        {
            foreach (var text in post.Fields.Values) yield return text;
            foreach (var item in post.Checklist) yield return item;
        }
    }

    public static bool IsWebLink(string? url) =>
        url != null
        && url.Length <= TemplateStep.MaxUrlLength
        && Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    #endregion

    #region Text forms used in modals

    // "Opponent *\nStream link" <-> fields. A trailing * marks a required field.
    // "key: Label" sets the key explicitly; otherwise it comes from the label.
    public static string FieldsToText(IEnumerable<TemplateField> fields) =>
        string.Join("\n", fields.Select(f =>
            (TemplateText.ToKey(f.Label) == f.Key ? f.Label : $"{f.Key}: {f.Label}") + (f.Required ? " *" : string.Empty)));

    public static List<TemplateField> ParseFieldsText(string? text, out List<string> problems)
    {
        problems = new List<string>();
        var fields = new List<TemplateField>();

        foreach (var raw in SplitLines(text))
        {
            var line = raw;
            var required = line.EndsWith('*');
            if (required) line = line.TrimEnd('*').TrimEnd();

            string? key;
            string label;
            var colon = line.IndexOf(':');
            if (colon > 0 && TemplateText.IsValidKey(line[..colon].Trim()))
            {
                key = line[..colon].Trim();
                label = line[(colon + 1)..].Trim();
            }
            else
            {
                label = line;
                key = TemplateText.ToKey(label);
            }

            if (key == null || label.Length == 0)
            {
                problems.Add($"Couldn't read the field \"{raw}\".");
                continue;
            }

            fields.Add(new TemplateField { Key = key, Label = label, Required = required });
        }

        return fields;
    }

    // One item per line; leading "- ", "• ", "[ ]" or "1." are dropped
    public static List<string> ParseChecklist(string? text) =>
        SplitLines(text)
            .Select(l => System.Text.RegularExpressions.Regex.Replace(l, @"^(\[\s?[xX]?\]|[-•*]|\d+[.)])\s*", string.Empty).Trim())
            .Where(l => l.Length > 0)
            .ToList();

    public static string ChecklistToText(IEnumerable<string> items) => string.Join("\n", items);

    private static IEnumerable<string> SplitLines(string? text) =>
        (text ?? string.Empty).Replace("\r", string.Empty).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);

    public static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    public static string Short(string? text, int max = 40)
    {
        if (string.IsNullOrWhiteSpace(text)) return "untitled";
        text = text.Trim();
        return text.Length <= max ? text : text[..(max - 1)] + "…";
    }

    #endregion
}
