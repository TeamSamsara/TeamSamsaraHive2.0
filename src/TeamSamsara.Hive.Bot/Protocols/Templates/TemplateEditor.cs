using System.Text;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Protocols.Models;
using TeamSamsara.Hive.Core.Scheduling;

namespace TeamSamsara.Hive.Bot.Protocols.Templates;

// The editor panel behind /template show and /template new.
//
// Two views, both one ephemeral message that is edited in place:
//   Overview  steps list, "Edit a step" menu, Edit details / Add step / Sort / Delete
//   Step      instructions, owner, due time, links, post text per platform
//             "Edit post text" menu, Edit step / Phase & links / Copy-paste view,
//             Move up / Move down / Delete step / Back
//
// Custom ids: proto:tpl:{action}:{templateId}[:{stepId}[:{platform}]]
// Every click reloads the template, so a panel left open never saves stale data.
public partial class TemplateCommandHandler
{
    private static readonly Color PanelColor = new(0xB8F24A);
    private static readonly Color WarningColor = new(0xF2B84A);

    private sealed record Panel(string? Content, Embed? Embed, MessageComponent Components);

    private static string Id(string action, string templateId, string? stepId = null, string? platform = null)
    {
        var id = IdPrefix + action + ":" + templateId;
        if (stepId != null) id += ":" + stepId;
        if (platform != null) id += ":" + platform;
        return id;
    }

    private static (string Action, string TemplateId, string? StepId, string? Platform) ParseId(string customId)
    {
        var parts = customId[IdPrefix.Length..].Split(':');
        return (parts[0],
            parts.Length > 1 ? parts[1] : string.Empty,
            parts.Length > 2 ? parts[2] : null,
            parts.Length > 3 ? parts[3] : null);
    }

    #region Buttons and menus

    private async Task HandleComponentAsync(SocketMessageComponent component)
    {
        var (action, templateId, stepId, _) = ParseId(component.Data.CustomId);

        if (action is "impok" or "impno")
        {
            await HandleImportButtonAsync(component, templateId, action == "impok");
            return;
        }

        var template = await _templates.GetAsync(templateId);
        if (template == null)
        {
            await UpdateWithTextAsync(component, "That template no longer exists. See `/template list`.");
            return;
        }

        // Menus carry the step or platform in the chosen value
        var picked = component.Data.Values?.FirstOrDefault();
        if (action == "step") stepId = picked;

        TemplateStep? step = null;
        if (stepId != null)
        {
            step = template.FindStep(stepId);
            if (step == null)
            {
                await UpdatePanelAsync(component, await BuildOverviewAsync(template, "That step no longer exists."));
                return;
            }
        }

        switch (action)
        {
            case "home":
                await UpdatePanelAsync(component, await BuildOverviewAsync(template, null));
                break;

            case "details":
                await component.RespondWithModalAsync(BuildDetailsModal(template));
                break;

            case "addstep":
                if (template.Steps.Count >= ProtocolTemplate.MaxSteps)
                {
                    await UpdatePanelAsync(component, await BuildOverviewAsync(template,
                        $"This template already has {ProtocolTemplate.MaxSteps} steps, the most it can hold. Split it into two templates."));
                    break;
                }
                await component.RespondWithModalAsync(await BuildStepModalAsync(template, null));
                break;

            case "sort":
                template.Steps = template.Steps.OrderBy(s => SortKey(s.Due)).ToList();
                Stamp(template, component.User);
                await _templates.SaveAsync(template);
                await UpdatePanelAsync(component, await BuildOverviewAsync(template, "Steps sorted by due time."));
                break;

            case "del":
                await UpdatePanelAsync(component, BuildDeleteConfirm(template));
                break;

            case "delyes":
                // Phase 3: refuse while planned events still use this template
                await _templates.DeleteAsync(template.Id);
                _logger.LogInformation("Template {Id} deleted by {Username}", template.Id, component.User.Username);
                await UpdateWithTextAsync(component, $"Deleted **{template.Name}**.");
                break;

            case "step":
            case "sview":
                await UpdatePanelAsync(component, await BuildStepPanelAsync(template, step!, null));
                break;

            case "sbasic":
                await component.RespondWithModalAsync(await BuildStepModalAsync(template, step));
                break;

            case "slinks":
                await component.RespondWithModalAsync(BuildLinksModal(template, step!));
                break;

            case "spost":
                var platform = Platforms.Find(picked);
                if (platform == null)
                {
                    await component.RespondAsync("Unknown platform.", ephemeral: true);
                    break;
                }
                await component.RespondWithModalAsync(BuildPostModal(template, step!, platform));
                break;

            case "sup":
            case "sdown":
                var index = template.Steps.IndexOf(template.Steps.First(s => s.Id == step!.Id));
                var target = action == "sup" ? index - 1 : index + 1;
                if (target >= 0 && target < template.Steps.Count)
                {
                    (template.Steps[index], template.Steps[target]) = (template.Steps[target], template.Steps[index]);
                    Stamp(template, component.User);
                    await _templates.SaveAsync(template);
                }
                await UpdatePanelAsync(component, await BuildStepPanelAsync(template, template.FindStep(step!.Id)!, null));
                break;

            case "sdel":
                await UpdatePanelAsync(component, new Panel(
                    $"Delete the step **{step!.Title}**? Its instructions and post text go with it.",
                    null,
                    new ComponentBuilder()
                        .WithButton("Delete step", Id("sdelyes", template.Id, step.Id), ButtonStyle.Danger)
                        .WithButton("Cancel", Id("sview", template.Id, step.Id), ButtonStyle.Secondary)
                        .Build()));
                break;

            case "sdelyes":
                template.Steps.RemoveAll(s => s.Id == step!.Id);
                Stamp(template, component.User);
                await _templates.SaveAsync(template);
                await UpdatePanelAsync(component, await BuildOverviewAsync(template, $"Deleted the step **{step!.Title}**."));
                break;

            case "scopy":
                await SendCopyPasteViewAsync(component, step!);
                break;

            default:
                await component.RespondAsync("That button isn't supported any more. Run the command again.", ephemeral: true);
                break;
        }
    }

    // Due offsets as a sortable number (minutes from start, for a typical evening event)
    private static double SortKey(string due)
    {
        if (!DueOffset.TryParse(due, out var offset, out _)) return 0;
        var reference = new DateTime(2030, 6, 12, 19, 0, 0, DateTimeKind.Utc);
        return (offset.ResolveUtc(reference, TimeZoneInfo.Utc) - reference).TotalMinutes;
    }

    // Every platform's text in code blocks, so each one has a copy button on desktop
    private async Task SendCopyPasteViewAsync(SocketMessageComponent component, TemplateStep step)
    {
        var blocks = new List<string>
        {
            $"**{step.Title}**: copy-paste text. `{{placeholders}}` are filled in when an event is planned; `[CAPITALS]` are yours to write."
        };

        foreach (var platform in Platforms.All.Where(p => step.Posts.ContainsKey(p.Key)))
        {
            var post = step.Posts[platform.Key];
            blocks.Add($"## {platform.Emoji} {platform.Name}");
            foreach (var field in platform.Fields)
            {
                var text = post.Fields.GetValueOrDefault(field.Key);
                if (string.IsNullOrWhiteSpace(text)) continue;

                var pieces = SplitText(text.Replace("```", "`​``"), 1800);
                for (var i = 0; i < pieces.Count; i++)
                {
                    var label = pieces.Count > 1 ? $"{field.Label} ({i + 1}/{pieces.Count})" : field.Label;
                    blocks.Add($"**{label}**\n```\n{pieces[i]}\n```");
                }
            }

            if (post.Checklist.Count > 0)
            {
                blocks.Add(string.Join("\n", post.Checklist.Select(item => $"☐ {item}")));
            }
        }

        var messages = PackMessages(blocks, 1950);
        await component.RespondAsync(messages[0], allowedMentions: AllowedMentions.None, ephemeral: true);
        foreach (var message in messages.Skip(1))
        {
            await component.FollowupAsync(message, allowedMentions: AllowedMentions.None, ephemeral: true);
        }
    }

    // Splits at line breaks where possible
    private static List<string> SplitText(string text, int max)
    {
        var pieces = new List<string>();
        while (text.Length > max)
        {
            var cut = text.LastIndexOf('\n', max);
            if (cut < max / 2) cut = max;
            pieces.Add(text[..cut].TrimEnd());
            text = text[cut..].TrimStart('\n');
        }
        pieces.Add(text);
        return pieces;
    }

    private static List<string> PackMessages(IEnumerable<string> blocks, int max)
    {
        var messages = new List<string>();
        var current = new StringBuilder();
        foreach (var block in blocks)
        {
            if (current.Length > 0 && current.Length + block.Length + 1 > max)
            {
                messages.Add(current.ToString());
                current.Clear();
            }
            if (current.Length > 0) current.Append('\n');
            current.Append(block);
        }
        if (current.Length > 0) messages.Add(current.ToString());
        return messages;
    }

    #endregion

    #region Forms (modals)

    private async Task HandleModalAsync(SocketModal modal)
    {
        var (action, templateId, stepId, platformKey) = ParseId(modal.Data.CustomId);
        var values = modal.Data.Components.ToDictionary(c => c.CustomId, c => c.Value?.Trim() ?? string.Empty);
        string Value(string key) => values.GetValueOrDefault(key) ?? string.Empty;

        if (action == "m-details" && templateId == "new")
        {
            await CreateTemplateFromModalAsync(modal, Value);
            return;
        }

        var template = await _templates.GetAsync(templateId);
        if (template == null)
        {
            await modal.RespondAsync("That template no longer exists. Nothing was saved.", ephemeral: true);
            return;
        }

        var notices = new List<string>();
        var ownerNames = (await _settings.GetAsync()).OwnerRoles.Select(o => o.Name).ToList();

        if (action == "m-details")
        {
            await ApplyDetailsAsync(template, Value, notices);
            Stamp(template, modal.User);
            await _templates.SaveAsync(template);
            await ShowPanelAsync(modal, await BuildOverviewAsync(template, Notice("Saved.", notices)));
            return;
        }

        TemplateStep? step;
        if (action == "m-step" && stepId == "new")
        {
            if (template.Steps.Count >= ProtocolTemplate.MaxSteps)
            {
                await modal.RespondAsync($"This template already has {ProtocolTemplate.MaxSteps} steps. Nothing was saved.", ephemeral: true);
                return;
            }
            step = new TemplateStep();
            template.Steps.Add(step);
        }
        else
        {
            step = stepId == null ? null : template.FindStep(stepId);
            if (step == null)
            {
                await modal.RespondAsync("That step no longer exists. Nothing was saved.", ephemeral: true);
                return;
            }
        }

        switch (action)
        {
            case "m-step":
                step.Title = Value("title");
                step.Owner = NormalizeOwner(Value("owner"), ownerNames);
                step.Where = TemplateRules.NullIfBlank(Value("where"));
                step.Instructions = TemplateRules.NullIfBlank(Value("instructions"));
                if (DueOffset.TryParse(Value("due"), out var due, out var dueError))
                    step.Due = due.ToSpec();
                else
                    notices.Add($"Due time not understood ({dueError}), so it stays **{DescribeOffset(step.Due)}**.");
                break;

            case "m-links":
                step.Phase = TemplateRules.NullIfBlank(Value("phase"));
                SetLink(Value("assets"), "assets link", url => step.AssetsUrl = url, notices);
                SetLink(Value("example"), "example link", url => step.ExampleUrl = url, notices);
                break;

            case "m-post":
                var platform = Platforms.Find(platformKey);
                if (platform == null)
                {
                    await modal.RespondAsync("Unknown platform. Nothing was saved.", ephemeral: true);
                    return;
                }
                ApplyPost(step, platform, Value, notices);
                break;

            default:
                await modal.RespondAsync("That form isn't supported any more. Nothing was saved.", ephemeral: true);
                return;
        }

        Stamp(template, modal.User);
        await _templates.SaveAsync(template);
        await ShowPanelAsync(modal, await BuildStepPanelAsync(template, step, Notice("Saved.", notices)));
    }

    private async Task CreateTemplateFromModalAsync(SocketModal modal, Func<string, string> value)
    {
        var name = value("name");
        var problem = await CheckNewNameAsync(name, exceptId: null);
        if (problem != null)
        {
            await modal.RespondAsync(problem + " Nothing was saved.", ephemeral: true);
            return;
        }

        var template = new ProtocolTemplate { Id = ProtocolTemplate.ToId(name)!, Name = name };
        var notices = new List<string>();
        await ApplyDetailsAsync(template, value, notices);
        Stamp(template, modal.User);
        await _templates.SaveAsync(template);
        _logger.LogInformation("Template {Id} created by {Username}", template.Id, modal.User.Username);

        await ShowPanelAsync(modal, await BuildOverviewAsync(template,
            Notice($"Created **{template.Name}**. Now add its steps.", notices)));
    }

    // Applies what's valid and explains anything that was kept as it was
    private async Task ApplyDetailsAsync(ProtocolTemplate template, Func<string, string> value, List<string> notices)
    {
        var name = value("name");
        if (!string.Equals(name, template.Name, StringComparison.Ordinal))
        {
            var problem = await CheckNewNameAsync(name, exceptId: template.Id);
            if (problem == null) template.Name = name;
            else notices.Add(problem + " The name wasn't changed.");
        }

        template.Description = TemplateRules.NullIfBlank(value("description"));

        if (DueOffset.TryParse(value("opens"), out var opens, out var opensError))
            template.Opens = opens.ToSpec();
        else
            notices.Add($"\"Thread opens\" not understood ({opensError}), so it stays **{DescribeOffset(template.Opens)}**.");

        var fields = TemplateRules.ParseFieldsText(value("fields"), out var fieldProblems);
        fieldProblems.AddRange(TemplateRules.FieldErrors(fields));
        if (fieldProblems.Count == 0)
            template.Fields = fields;
        else
            notices.Add("The fields weren't changed: " + string.Join(" ", fieldProblems));
    }

    private static void SetLink(string text, string label, Action<string?> apply, List<string> notices)
    {
        if (text.Length == 0)
            apply(null);
        else if (TemplateRules.IsWebLink(text))
            apply(text);
        else
            notices.Add($"The {label} must start with http:// or https://, so it wasn't changed.");
    }

    private static void ApplyPost(TemplateStep step, PlatformDefinition platform, Func<string, string> value, List<string> notices)
    {
        var post = new PostTemplate();
        foreach (var field in platform.Fields)
        {
            var text = value(field.Key);
            if (text.Length > 0) post.Fields[field.Key] = text;
        }

        var checklist = TemplateRules.ParseChecklist(value("checklist"));
        if (checklist.Count > Platforms.MaxChecklistItems)
        {
            notices.Add($"Only the first {Platforms.MaxChecklistItems} checklist items were kept.");
            checklist = checklist.Take(Platforms.MaxChecklistItems).ToList();
        }
        if (checklist.Any(i => i.Length > Platforms.MaxChecklistItemLength))
        {
            notices.Add($"Checklist items over {Platforms.MaxChecklistItemLength} characters were shortened.");
            checklist = checklist.Select(i => i.Length > Platforms.MaxChecklistItemLength ? i[..Platforms.MaxChecklistItemLength] : i).ToList();
        }
        post.Checklist = checklist;

        if (post.IsEmpty)
        {
            if (step.Posts.Remove(platform.Key)) notices.Add($"Everything was empty, so the {platform.Name} text was removed.");
        }
        else
        {
            step.Posts[platform.Key] = post;
        }
    }

    private static Modal BuildDetailsModal(ProtocolTemplate? template)
    {
        return new ModalBuilder()
            .WithTitle(template == null ? "New template" : TemplateRules.Short("Edit " + template.Name, 45))
            .WithCustomId(Id("m-details", template?.Id ?? "new"))
            .AddTextInput("Name", "name", TextInputStyle.Short, "Gameday, Voice comms video, Sponsor post...",
                1, ProtocolTemplate.MaxNameLength, true, template?.Name)
            .AddTextInput("Description", "description", TextInputStyle.Paragraph, "What this template is for (optional)",
                null, ProtocolTemplate.MaxDescriptionLength, false, template?.Description)
            .AddTextInput("Thread opens", "opens", TextInputStyle.Short, "e.g. 2 days before 9:00, day of 12:00",
                1, 60, true, DescribeOffset(template?.Opens ?? ProtocolTemplate.DefaultOpens))
            .AddTextInput("Info per event (one per line, * = required)", "fields", TextInputStyle.Paragraph,
                "Opponent *\nStream link\nUsed in text as {opponent}, {stream_link}",
                null, 1000, false, template == null ? null : TemplateRules.NullIfBlank(TemplateRules.FieldsToText(template.Fields)))
            .Build();
    }

    private async Task<Modal> BuildStepModalAsync(ProtocolTemplate template, TemplateStep? step)
    {
        var owners = (await _settings.GetAsync()).OwnerRoles.Select(o => o.Name).ToList();
        var ownerHint = owners.Count > 0
            ? TemplateRules.Short("One of: " + string.Join(", ", owners), 100)
            : "A name from /protocol role list, e.g. Design";

        return new ModalBuilder()
            .WithTitle(step == null ? TemplateRules.Short("Add step to " + template.Name, 45) : TemplateRules.Short("Edit: " + step.Title, 45))
            .WithCustomId(Id("m-step", template.Id, step?.Id ?? "new"))
            .AddTextInput("Step", "title", TextInputStyle.Short, "e.g. Match-day graphic",
                1, TemplateStep.MaxTitleLength, true, step?.Title)
            .AddTextInput("Owner", "owner", TextInputStyle.Short, ownerHint,
                1, TemplateStep.MaxOwnerLength, true, step?.Owner)
            .AddTextInput("Due", "due", TextInputStyle.Short, "e.g. 2 hours before, day of 12:00, 2 days before 9:00",
                1, 60, true, step == null ? null : DescribeOffset(step.Due))
            .AddTextInput("Where", "where", TextInputStyle.Short, "e.g. Twitter, #announcements (optional)",
                null, TemplateStep.MaxWhereLength, false, step?.Where)
            .AddTextInput("Instructions", "instructions", TextInputStyle.Paragraph, "How to do it, step by step (optional)",
                null, TemplateStep.MaxInstructionsLength, false, step?.Instructions)
            .Build();
    }

    private static Modal BuildLinksModal(ProtocolTemplate template, TemplateStep step) =>
        new ModalBuilder()
            .WithTitle(TemplateRules.Short("Links: " + step.Title, 45))
            .WithCustomId(Id("m-links", template.Id, step.Id))
            .AddTextInput("Phase", "phase", TextInputStyle.Short, "Groups steps, e.g. Before, Live, After (optional)",
                null, TemplateStep.MaxPhaseLength, false, step.Phase)
            .AddTextInput("Assets link", "assets", TextInputStyle.Short, "https://drive.google.com/... (PSD, logos)",
                null, TemplateStep.MaxUrlLength, false, step.AssetsUrl)
            .AddTextInput("Example link", "example", TextInputStyle.Short, "A past post that did this well",
                null, TemplateStep.MaxUrlLength, false, step.ExampleUrl)
            .Build();

    private static Modal BuildPostModal(ProtocolTemplate template, TemplateStep step, PlatformDefinition platform)
    {
        step.Posts.TryGetValue(platform.Key, out var post);
        var builder = new ModalBuilder()
            .WithTitle(TemplateRules.Short($"{platform.Name}: {step.Title}", 45))
            .WithCustomId(Id("m-post", template.Id, step.Id, platform.Key));

        foreach (var field in platform.Fields)
        {
            var current = post?.Fields.GetValueOrDefault(field.Key);
            builder.AddTextInput(field.InputLabel, field.Key,
                field.Multiline ? TextInputStyle.Paragraph : TextInputStyle.Short,
                field.Placeholder ?? "Use {opponent}-style placeholders and [CAPITALS] for blanks",
                null, PlatformField.ModalMaxLength, false,
                current?.Length > PlatformField.ModalMaxLength ? current[..PlatformField.ModalMaxLength] : current);
        }

        return builder
            .AddTextInput("Checklist (one item per line)", "checklist", TextInputStyle.Paragraph, "Attach the graphic\nTag the sponsor",
                null, 2000, false, post == null ? null : TemplateRules.NullIfBlank(TemplateRules.ChecklistToText(post.Checklist)))
            .Build();
    }

    #endregion

    #region Panels

    private async Task<Panel> BuildOverviewAsync(ProtocolTemplate template, string? notice)
    {
        var settings = await _settings.GetAsync();
        var ownerNames = settings.OwnerRoles.Select(o => o.Name).ToList();
        var check = TemplateRules.Check(template, ownerNames);

        var text = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(template.Description)) text.AppendLine(template.Description).AppendLine();
        text.AppendLine($"Thread opens **{DescribeOffset(template.Opens)}**");
        text.AppendLine(template.Fields.Count == 0
            ? "Asks for: nothing yet (add info like Opponent in **Edit details**)"
            : "Asks for: " + string.Join(", ", template.Fields.Select(f => $"{f.Label}{(f.Required ? " *" : "")} `{{{f.Key}}}`")));
        text.AppendLine().AppendLine($"**Steps ({template.Steps.Count})**");

        if (template.Steps.Count == 0)
        {
            text.AppendLine("No steps yet. Press **Add step**.");
        }
        for (var i = 0; i < template.Steps.Count; i++)
        {
            var s = template.Steps[i];
            var phase = s.Phase != null ? $"`{s.Phase}` " : string.Empty;
            var owner = ownerNames.Contains(s.Owner, StringComparer.OrdinalIgnoreCase) ? s.Owner : $"{s.Owner} ⚠️";
            var platforms = string.Concat(Platforms.All.Where(p => s.Posts.ContainsKey(p.Key)).Select(p => p.Emoji));
            text.AppendLine($"`{i + 1}.` {phase}**{s.Title}** · {owner} · {DescribeOffset(s.Due)} {platforms}");
        }

        var embed = new EmbedBuilder()
            .WithTitle(TemplateRules.Short("📋 " + template.Name, 250))
            .WithColor(check.Warnings.Count > 0 ? WarningColor : PanelColor)
            .WithDescription(Truncate(text.ToString(), 4000))
            .WithFooter($"id: {template.Id}" + (template.UpdatedByUsername != null ? $" · last edit by {template.UpdatedByUsername}" : string.Empty));
        if (template.UpdatedAtUtc is { } updated) embed.WithTimestamp(new DateTimeOffset(updated, TimeSpan.Zero));
        AddWarnings(embed, check.Warnings.Concat(check.Errors));

        var components = new ComponentBuilder();
        if (template.Steps.Count > 0)
        {
            var menu = new SelectMenuBuilder()
                .WithCustomId(Id("step", template.Id))
                .WithPlaceholder("Edit a step…");
            for (var i = 0; i < template.Steps.Count; i++)
            {
                var s = template.Steps[i];
                menu.AddOption(TemplateRules.Short($"{i + 1}. {s.Title}", 100), s.Id,
                    TemplateRules.Short($"{s.Owner} · {DescribeOffset(s.Due)}", 100));
            }
            components.WithSelectMenu(menu, row: 0);
        }

        components
            .WithButton("Edit details", Id("details", template.Id), ButtonStyle.Primary, row: 1)
            .WithButton("Add step", Id("addstep", template.Id), ButtonStyle.Success,
                disabled: template.Steps.Count >= ProtocolTemplate.MaxSteps, row: 1)
            .WithButton("Sort by due time", Id("sort", template.Id), ButtonStyle.Secondary,
                disabled: template.Steps.Count < 2, row: 1)
            .WithButton("Delete template", Id("del", template.Id), ButtonStyle.Danger, row: 1);

        return new Panel(notice, embed.Build(), components.Build());
    }

    private async Task<Panel> BuildStepPanelAsync(ProtocolTemplate template, TemplateStep step, string? notice)
    {
        var settings = await _settings.GetAsync();
        var index = template.Steps.FindIndex(s => s.Id == step.Id);
        var check = new TemplateCheck();
        TemplateRules.CheckStep(step, "This step", settings.OwnerRoles.Select(o => o.Name).ToList(), check);

        var owner = settings.FindOwner(step.Owner) is { } mapped
            ? $"{step.Owner} → <@&{mapped.RoleId}>"
            : $"{step.Owner} ⚠️ no role yet";

        var links = new List<string>();
        if (step.AssetsUrl != null) links.Add($"[Assets]({step.AssetsUrl})");
        if (step.ExampleUrl != null) links.Add($"[Example]({step.ExampleUrl})");

        var embed = new EmbedBuilder()
            .WithTitle(TemplateRules.Short($"Step {index + 1} of {template.Steps.Count}: {step.Title}", 250))
            .WithColor(check.Warnings.Count > 0 ? WarningColor : PanelColor)
            .WithDescription(string.IsNullOrWhiteSpace(step.Instructions)
                ? "_No instructions yet. Press **Edit step** to add some._"
                : Truncate(step.Instructions, 1000))
            .AddField("Owner", owner, inline: true)
            .AddField("Due", DescribeOffset(step.Due), inline: true)
            .AddField("Where", step.Where ?? "—", inline: true)
            .AddField("Phase", step.Phase ?? "—", inline: true)
            .AddField("Links", links.Count > 0 ? string.Join(" · ", links) : "None", inline: true)
            .WithFooter(TemplateRules.Short(template.Name, 100) + " · use {placeholders} like {opponent} in any text");

        var missing = new List<string>();
        foreach (var platform in Platforms.All)
        {
            if (!step.Posts.TryGetValue(platform.Key, out var post))
            {
                missing.Add(platform.Name);
                continue;
            }

            var preview = new StringBuilder();
            foreach (var field in platform.Fields)
            {
                var text = post.Fields.GetValueOrDefault(field.Key);
                if (string.IsNullOrWhiteSpace(text)) continue;
                preview.Append($"**{field.Label}**\n```\n{Truncate(text.Replace("```", "`​``"), 150)}\n```");
            }
            if (post.Checklist.Count > 0) preview.Append($"☐ {post.Checklist.Count} checklist item{(post.Checklist.Count == 1 ? "" : "s")}");
            embed.AddField($"{platform.Emoji} {platform.Name}", preview.Length > 0 ? Truncate(preview.ToString(), 1000) : "(empty)");
        }

        if (missing.Count > 0)
        {
            embed.AddField("Post text", $"Not set for {string.Join(", ", missing)}. Pick a platform in the menu to add it.");
        }
        AddWarnings(embed, check.Warnings.Concat(check.Errors));

        var menu = new SelectMenuBuilder()
            .WithCustomId(Id("spost", template.Id, step.Id))
            .WithPlaceholder("Edit post text for…");
        foreach (var platform in Platforms.All)
        {
            var set = step.Posts.ContainsKey(platform.Key);
            menu.AddOption(platform.Name, platform.Key, set ? "Set: click to edit" : "Not set: click to add", new Emoji(platform.Emoji));
        }

        var components = new ComponentBuilder()
            .WithSelectMenu(menu, row: 0)
            .WithButton("Edit step", Id("sbasic", template.Id, step.Id), ButtonStyle.Primary, row: 1)
            .WithButton("Phase & links", Id("slinks", template.Id, step.Id), ButtonStyle.Secondary, row: 1)
            .WithButton("Copy-paste view", Id("scopy", template.Id, step.Id), ButtonStyle.Secondary,
                disabled: step.Posts.Count == 0, row: 1)
            .WithButton("Move up", Id("sup", template.Id, step.Id), ButtonStyle.Secondary, new Emoji("⬆️"),
                disabled: index <= 0, row: 2)
            .WithButton("Move down", Id("sdown", template.Id, step.Id), ButtonStyle.Secondary, new Emoji("⬇️"),
                disabled: index >= template.Steps.Count - 1, row: 2)
            .WithButton("Delete step", Id("sdel", template.Id, step.Id), ButtonStyle.Danger, row: 2)
            .WithButton("Back to template", Id("home", template.Id), ButtonStyle.Secondary, row: 2);

        return new Panel(notice, embed.Build(), components.Build());
    }

    private static Panel BuildDeleteConfirm(ProtocolTemplate template) => new(
        $"Delete **{template.Name}** and its {template.Steps.Count} step(s)? This can't be undone; `/template export` it first if you might want it back.",
        null,
        new ComponentBuilder()
            .WithButton("Delete template", Id("delyes", template.Id), ButtonStyle.Danger)
            .WithButton("Cancel", Id("home", template.Id), ButtonStyle.Secondary)
            .Build());

    private static void AddWarnings(EmbedBuilder embed, IEnumerable<string> warnings)
    {
        var list = warnings.Distinct().ToList();
        if (list.Count == 0) return;

        var text = string.Join("\n", list.Take(8).Select(w => "• " + w));
        if (list.Count > 8) text += $"\n…and {list.Count - 8} more";
        embed.AddField($"⚠️ To check ({list.Count})", Truncate(text, 1024));
    }

    private static string? Notice(string saved, List<string> notices) =>
        notices.Count == 0 ? saved : saved + " Heads-up:\n• " + string.Join("\n• ", notices);

    #endregion

    #region Showing panels

    private static Task UpdatePanelAsync(SocketMessageComponent component, Panel panel) =>
        component.UpdateAsync(p => Apply(p, panel));

    private static Task UpdateWithTextAsync(SocketMessageComponent component, string text) =>
        component.UpdateAsync(p => Apply(p, new Panel(text, null, new ComponentBuilder().Build())));

    // A form opened from a panel edits that panel; one opened from a slash
    // command (/template new) answers with a new panel
    private static Task ShowPanelAsync(SocketModal modal, Panel panel)
    {
        if (modal.Message != null)
        {
            return modal.UpdateAsync(p => Apply(p, panel));
        }

        return modal.RespondAsync(panel.Content, embed: panel.Embed, components: panel.Components,
            allowedMentions: AllowedMentions.None, ephemeral: true);
    }

    private static void Apply(MessageProperties p, Panel panel)
    {
        p.Content = Truncate(panel.Content, 2000);
        p.Embeds = panel.Embed != null ? new[] { panel.Embed } : Array.Empty<Embed>();
        p.Components = panel.Components;
        p.AllowedMentions = AllowedMentions.None;
    }

    #endregion
}
