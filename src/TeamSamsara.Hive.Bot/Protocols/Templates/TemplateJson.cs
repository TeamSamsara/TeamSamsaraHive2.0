using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using TeamSamsara.Hive.Bot.Protocols.Models;
using TeamSamsara.Hive.Core.Scheduling;

namespace TeamSamsara.Hive.Bot.Protocols.Templates;

// The JSON file used by /template export and /template import.
//
// {
//   "format": "samsara-protocol-templates",
//   "version": 1,
//   "templates": [
//     {
//       "name": "Gameday",
//       "description": "Every official match",
//       "opens": "2 days before 9:00",
//       "fields": [ { "key": "opponent", "label": "Opponent", "required": true } ],
//       "steps": [
//         {
//           "title": "Match-day graphic",
//           "phase": "Before",
//           "owner": "Design",
//           "due": "day of 12:00",
//           "where": "Twitter",
//           "instructions": "Use the PSD in the drive...",
//           "assetsUrl": "https://drive.google.com/...",
//           "exampleUrl": "https://x.com/...",
//           "posts": {
//             "twitter": { "post": "GAMEDAY vs {opponent} ...", "checklist": [ "Attach the graphic" ] },
//             "discord": "Shorthand: a plain string fills the platform's first field"
//           }
//         }
//       ]
//     }
//   ]
// }
//
// Times can be written the friendly way ("2 hours before") and are stored in
// the short form ("-2h"). A single template object (without the wrapper) is
// also accepted.
public static class TemplateJson
{
    public const string FormatName = "samsara-protocol-templates";
    public const int Version = 1;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        // Keep accents, emoji and quotes readable in the exported file
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    #region Export

    public static string Export(IEnumerable<ProtocolTemplate> templates)
    {
        var root = new JsonObject
        {
            ["format"] = FormatName,
            ["version"] = Version,
            ["templates"] = new JsonArray(templates.Select(ToJson).ToArray<JsonNode?>())
        };
        return KeepEmoji(root.ToJsonString(WriteOptions));
    }

    // The relaxed encoder still writes emoji as "\uD83C\uDFAE"; put them back
    // as real characters so post text stays readable when editing the file
    private static readonly System.Text.RegularExpressions.Regex EscapedSurrogatePair = new(
        @"\\u(D[89AB][0-9A-F]{2})\\u(D[C-F][0-9A-F]{2})",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string KeepEmoji(string json) =>
        EscapedSurrogatePair.Replace(json, m => new string(new[]
        {
            (char)Convert.ToInt32(m.Groups[1].Value, 16),
            (char)Convert.ToInt32(m.Groups[2].Value, 16)
        }));

    private static JsonObject ToJson(ProtocolTemplate t)
    {
        var obj = new JsonObject { ["name"] = t.Name };
        AddIfSet(obj, "description", t.Description);
        obj["opens"] = t.Opens;
        obj["fields"] = new JsonArray(t.Fields.Select(f => (JsonNode?)new JsonObject
        {
            ["key"] = f.Key,
            ["label"] = f.Label,
            ["required"] = f.Required
        }).ToArray());
        obj["steps"] = new JsonArray(t.Steps.Select(s =>
        {
            var step = new JsonObject { ["id"] = s.Id, ["title"] = s.Title };
            AddIfSet(step, "phase", s.Phase);
            step["owner"] = s.Owner;
            step["due"] = s.Due;
            AddIfSet(step, "where", s.Where);
            AddIfSet(step, "instructions", s.Instructions);
            AddIfSet(step, "assetsUrl", s.AssetsUrl);
            AddIfSet(step, "exampleUrl", s.ExampleUrl);

            if (s.Posts.Count > 0)
            {
                var posts = new JsonObject();
                foreach (var platform in Platforms.All.Where(p => s.Posts.ContainsKey(p.Key)))
                {
                    var post = s.Posts[platform.Key];
                    var postObj = new JsonObject();
                    foreach (var field in platform.Fields)
                    {
                        AddIfSet(postObj, field.Key, post.Fields.GetValueOrDefault(field.Key));
                    }
                    if (post.Checklist.Count > 0)
                    {
                        postObj["checklist"] = new JsonArray(post.Checklist.Select(i => (JsonNode?)i).ToArray());
                    }
                    posts[platform.Key] = postObj;
                }
                step["posts"] = posts;
            }

            return (JsonNode?)step;
        }).ToArray());
        return obj;
    }

    private static void AddIfSet(JsonObject obj, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) obj[key] = value;
    }

    #endregion

    #region Import

    // Reads the file into templates. Problems that stop a template from being
    // understood at all are returned in errors; rule checks happen afterwards
    // with TemplateRules.Check.
    public static List<ProtocolTemplate> Import(string json, out List<string> errors)
    {
        errors = new List<string>();
        var templates = new List<ProtocolTemplate>();

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
        }
        catch (JsonException ex)
        {
            errors.Add($"That isn't valid JSON: {ex.Message}");
            return templates;
        }

        IEnumerable<JsonNode?> items = root switch
        {
            JsonObject obj when obj["templates"] is JsonArray array => array,
            JsonObject obj when obj["name"] != null => new[] { (JsonNode?)obj },
            JsonArray array => array,
            _ => Array.Empty<JsonNode?>()
        };

        var index = 0;
        foreach (var item in items)
        {
            index++;
            if (item is not JsonObject obj)
            {
                errors.Add($"Template {index} isn't an object.");
                continue;
            }

            try
            {
                templates.Add(ReadTemplate(obj, $"Template {index}", errors));
            }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException)
            {
                errors.Add($"Template {index}: {ex.Message}");
            }
        }

        if (templates.Count == 0 && errors.Count == 0)
        {
            errors.Add("No templates found. The file should have a \"templates\" list (see /template export for an example).");
        }

        return templates;
    }

    private static ProtocolTemplate ReadTemplate(JsonObject obj, string label, List<string> errors)
    {
        var name = Str(obj, "name")?.Trim() ?? string.Empty;
        label = name.Length > 0 ? $"\"{name}\"" : label;

        var template = new ProtocolTemplate
        {
            Name = name,
            Id = ProtocolTemplate.ToId(name) ?? string.Empty,
            Description = TemplateRules.NullIfBlank(Str(obj, "description")),
            Opens = NormalizeOffset(Str(obj, "opens"), ProtocolTemplate.DefaultOpens)
        };

        if (obj["fields"] is JsonArray fields)
        {
            foreach (var node in fields)
            {
                switch (node)
                {
                    // "Opponent *" shorthand
                    case JsonValue value when value.TryGetValue<string>(out var text):
                        template.Fields.AddRange(TemplateRules.ParseFieldsText(text, out var problems));
                        errors.AddRange(problems.Select(p => $"{label}: {p}"));
                        break;
                    case JsonObject f:
                        var fieldLabel = Str(f, "label")?.Trim() ?? Str(f, "key") ?? string.Empty;
                        template.Fields.Add(new TemplateField
                        {
                            Key = Str(f, "key")?.Trim() ?? Core.Utilities.TemplateText.ToKey(fieldLabel) ?? string.Empty,
                            Label = fieldLabel,
                            Required = f["required"] is JsonValue r && r.TryGetValue<bool>(out var required) && required
                        });
                        break;
                }
            }
        }

        if (obj["steps"] is JsonArray steps)
        {
            var stepIndex = 0;
            foreach (var node in steps)
            {
                stepIndex++;
                if (node is JsonObject s)
                {
                    template.Steps.Add(ReadStep(s, $"{label} step {stepIndex}", errors));
                }
            }
        }

        return template;
    }

    private static TemplateStep ReadStep(JsonObject s, string label, List<string> errors)
    {
        var step = new TemplateStep
        {
            Title = Str(s, "title")?.Trim() ?? string.Empty,
            Phase = TemplateRules.NullIfBlank(Str(s, "phase")),
            Owner = Str(s, "owner")?.Trim() ?? string.Empty,
            Due = NormalizeOffset(Str(s, "due"), "0"),
            Where = TemplateRules.NullIfBlank(Str(s, "where")),
            Instructions = TemplateRules.NullIfBlank(Str(s, "instructions")),
            AssetsUrl = TemplateRules.NullIfBlank(Str(s, "assetsUrl")),
            ExampleUrl = TemplateRules.NullIfBlank(Str(s, "exampleUrl"))
        };

        var id = Str(s, "id");
        if (!string.IsNullOrWhiteSpace(id) && id.Length <= 16 && id.All(char.IsLetterOrDigit))
        {
            step.Id = id;
        }

        if (s["posts"] is JsonObject posts)
        {
            foreach (var (platformKey, postNode) in posts)
            {
                var platform = Platforms.Find(platformKey);
                if (platform == null)
                {
                    errors.Add($"{label}: unknown platform \"{platformKey}\" (use: {string.Join(", ", Platforms.All.Select(p => p.Key))}).");
                    continue;
                }

                var post = new PostTemplate();
                if (postNode is JsonValue shorthand && shorthand.TryGetValue<string>(out var text))
                {
                    post.Fields[platform.Fields[0].Key] = text;
                }
                else if (postNode is JsonObject postObj)
                {
                    // Accept both { "post": "..." } and { "fields": { "post": "..." } }
                    var source = postObj["fields"] as JsonObject ?? postObj;
                    foreach (var (key, valueNode) in source)
                    {
                        if (key == "checklist" || key == "fields") continue;
                        if (valueNode is JsonValue v && v.TryGetValue<string>(out var value) && !string.IsNullOrEmpty(value))
                        {
                            post.Fields[key] = value;
                        }
                    }

                    if (postObj["checklist"] is JsonArray checklist)
                    {
                        // Same clean-up as the form: "- Tag sponsor" -> "Tag sponsor"
                        post.Checklist = TemplateRules.ParseChecklist(string.Join("\n", checklist
                            .Select(i => i is JsonValue v && v.TryGetValue<string>(out var item) ? item.Replace('\n', ' ') : null)
                            .Where(i => !string.IsNullOrWhiteSpace(i))));
                    }
                }

                if (!post.IsEmpty)
                {
                    step.Posts[platform.Key] = post;
                }
            }
        }

        return step;
    }

    // "2 hours before" -> "-2h". Unreadable values are kept as typed, so
    // TemplateRules.Check reports them with the other problems.
    private static string NormalizeOffset(string? text, string fallback) =>
        string.IsNullOrWhiteSpace(text) ? fallback
        : DueOffset.TryParse(text, out var offset, out _) ? offset.ToSpec()
        : text.Trim();

    private static string? Str(JsonObject obj, string key) =>
        obj[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    #endregion
}
