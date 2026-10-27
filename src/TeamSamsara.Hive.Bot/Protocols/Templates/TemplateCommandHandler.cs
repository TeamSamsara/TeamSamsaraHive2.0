using System.Collections.Concurrent;
using System.Text;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Integrations.Discord;
using TeamSamsara.Hive.Bot.Protocols.Models;
using TeamSamsara.Hive.Bot.Protocols.Repositories;
using TeamSamsara.Hive.Core.Scheduling;

namespace TeamSamsara.Hive.Bot.Protocols.Templates;

// /template: create and edit checklist templates.
//
//   /template new                 opens a form for name, description, timing and fields
//   /template list                every template with its step count
//   /template show template:      the editor panel (steps, buttons, post text)
//   /template copy template: name:
//   /template delete template:    asks to confirm
//   /template export [template:]  JSON file of one or all templates
//   /template import file:        reads a JSON file, previews, then asks to confirm
//
// Editing happens in the ephemeral panel (TemplateEditor.cs): buttons and
// forms for each step and each platform's copy-paste text.
public partial class TemplateCommandHandler
{
    private const string CommandName = "template";
    private const string IdPrefix = "proto:tpl:";
    private const int MaxImportBytes = 512 * 1024;
    private static readonly TimeSpan PendingImportLifetime = TimeSpan.FromMinutes(15);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private readonly DiscordIntegration _discord;
    private readonly ITemplateRepository _templates;
    private readonly IProtocolSettingsRepository _settings;
    private readonly ILogger<TemplateCommandHandler> _logger;

    // Imports waiting for the Import / Cancel button, by short token
    private readonly ConcurrentDictionary<string, PendingImport> _pendingImports = new();

    public TemplateCommandHandler(
        DiscordIntegration discord,
        ITemplateRepository templates,
        IProtocolSettingsRepository settings,
        ILogger<TemplateCommandHandler> logger)
    {
        _discord = discord ?? throw new ArgumentNullException(nameof(discord));
        _templates = templates ?? throw new ArgumentNullException(nameof(templates));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Registration

    public async Task RegisterAsync()
    {
        _discord.Interactions.OnSlashCommand(CommandName, HandleSlashCommandAsync);
        _discord.Interactions.OnAutocomplete(CommandName, HandleAutocompleteAsync);
        _discord.Interactions.OnComponent(IdPrefix, HandleComponentAsync);
        _discord.Interactions.OnModal(IdPrefix, HandleModalAsync);

        var guild = _discord.Client.Guilds.FirstOrDefault();
        if (guild == null)
        {
            _logger.LogWarning("No guild found for /template registration");
            return;
        }

        SlashCommandOptionBuilder TemplateOption(string description, bool required = true) => new SlashCommandOptionBuilder()
            .WithName("template").WithDescription(description)
            .WithType(ApplicationCommandOptionType.String).WithAutocomplete(true).WithRequired(required);

        var command = new SlashCommandBuilder()
            .WithName(CommandName)
            .WithDescription("Checklist templates for gamedays, videos, sponsors...")
            .WithDefaultMemberPermissions(GuildPermission.Administrator)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("new").WithDescription("Create a template")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("list").WithDescription("List every template")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("show").WithDescription("Open a template to view or edit it")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption(TemplateOption("Template to open")))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("copy").WithDescription("Copy a template under a new name")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption(TemplateOption("Template to copy"))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("name").WithDescription("Name for the copy")
                    .WithType(ApplicationCommandOptionType.String).WithRequired(true)
                    .WithMaxLength(ProtocolTemplate.MaxNameLength)))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("delete").WithDescription("Delete a template (asks to confirm)")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption(TemplateOption("Template to delete")))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("export").WithDescription("Download templates as a JSON file")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption(TemplateOption("Only this template (default: all)", required: false)))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("import").WithDescription("Add or replace templates from a JSON file")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("file").WithDescription("A .json file, e.g. from /template export")
                    .WithType(ApplicationCommandOptionType.Attachment).WithRequired(true)));

        try
        {
            await guild.CreateApplicationCommandAsync(command.Build());
            _logger.LogInformation("Registered /template command");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register /template command");
        }
    }

    #endregion

    #region Slash command

    private async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        var sub = command.Data.Options.FirstOrDefault();
        var options = sub?.Options.ToDictionary(o => o.Name, o => o.Value) ?? new Dictionary<string, object>();

        // A form has to be the first response, so this one isn't deferred
        if (sub?.Name == "new")
        {
            await command.RespondWithModalAsync(BuildDetailsModal(null));
            return;
        }

        await command.DeferAsync(ephemeral: true);

        switch (sub?.Name)
        {
            case "list":
                await HandleListAsync(command);
                break;
            case "show":
                await HandleShowAsync(command, options);
                break;
            case "copy":
                await HandleCopyAsync(command, options);
                break;
            case "delete":
                await HandleDeleteAsync(command, options);
                break;
            case "export":
                await HandleExportAsync(command, options);
                break;
            case "import":
                await HandleImportAsync(command, options);
                break;
            default:
                await command.FollowupAsync("Unknown subcommand", ephemeral: true);
                break;
        }
    }

    private async Task HandleListAsync(SocketSlashCommand command)
    {
        var templates = await _templates.GetAllAsync();
        if (templates.Count == 0)
        {
            await command.FollowupAsync("No templates yet. Create one with `/template new`, or `/template import` a file.", ephemeral: true);
            return;
        }

        var lines = templates.Select(t =>
        {
            var platforms = string.Concat(Platforms.All.Where(p => t.Steps.Any(s => s.Posts.ContainsKey(p.Key))).Select(p => p.Emoji));
            var description = string.IsNullOrWhiteSpace(t.Description) ? string.Empty : $" · {TemplateRules.Short(t.Description, 60)}";
            return $"**{t.Name}** · {t.Steps.Count} step{(t.Steps.Count == 1 ? "" : "s")} {platforms}{description}";
        });

        var embed = new EmbedBuilder()
            .WithTitle($"Templates ({templates.Count})")
            .WithColor(PanelColor)
            .WithDescription(Truncate(string.Join("\n", lines), 4000))
            .WithFooter("Open one with /template show")
            .Build();

        await command.FollowupAsync(embed: embed, ephemeral: true);
    }

    private async Task HandleShowAsync(SocketSlashCommand command, Dictionary<string, object> options)
    {
        var template = await FindTemplateAsync(options);
        if (template == null)
        {
            await command.FollowupAsync(NotFoundText(options), ephemeral: true);
            return;
        }

        var panel = await BuildOverviewAsync(template, notice: null);
        await command.FollowupAsync(panel.Content, embed: panel.Embed, components: panel.Components,
            allowedMentions: AllowedMentions.None, ephemeral: true);
    }

    private async Task HandleCopyAsync(SocketSlashCommand command, Dictionary<string, object> options)
    {
        var source = await FindTemplateAsync(options);
        if (source == null)
        {
            await command.FollowupAsync(NotFoundText(options), ephemeral: true);
            return;
        }

        var name = (options.GetValueOrDefault("name") as string ?? string.Empty).Trim();
        var problem = await CheckNewNameAsync(name, exceptId: null);
        if (problem != null)
        {
            await command.FollowupAsync(problem, ephemeral: true);
            return;
        }

        var copy = source.Copy();
        copy.Id = ProtocolTemplate.ToId(name)!;
        copy.Name = name;
        copy.CreatedAtUtc = null;
        Stamp(copy, command.User);
        await _templates.SaveAsync(copy);
        _logger.LogInformation("Template {Source} copied to {Id} by {Username}", source.Id, copy.Id, command.User.Username);

        var panel = await BuildOverviewAsync(copy, $"Copied **{source.Name}** to **{copy.Name}**.");
        await command.FollowupAsync(panel.Content, embed: panel.Embed, components: panel.Components,
            allowedMentions: AllowedMentions.None, ephemeral: true);
    }

    private async Task HandleDeleteAsync(SocketSlashCommand command, Dictionary<string, object> options)
    {
        var template = await FindTemplateAsync(options);
        if (template == null)
        {
            await command.FollowupAsync(NotFoundText(options), ephemeral: true);
            return;
        }

        var panel = BuildDeleteConfirm(template);
        await command.FollowupAsync(panel.Content, embed: panel.Embed, components: panel.Components, ephemeral: true);
    }

    #endregion

    #region Export and import

    private async Task HandleExportAsync(SocketSlashCommand command, Dictionary<string, object> options)
    {
        List<ProtocolTemplate> templates;
        string fileName;

        if (options.ContainsKey("template"))
        {
            var template = await FindTemplateAsync(options);
            if (template == null)
            {
                await command.FollowupAsync(NotFoundText(options), ephemeral: true);
                return;
            }
            templates = new List<ProtocolTemplate> { template };
            fileName = $"template-{template.Id}.json";
        }
        else
        {
            templates = await _templates.GetAllAsync();
            if (templates.Count == 0)
            {
                await command.FollowupAsync("There are no templates to export yet.", ephemeral: true);
                return;
            }
            fileName = $"templates-{DateTime.UtcNow:yyyy-MM-dd}.json";
        }

        var json = TemplateJson.Export(templates);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await command.FollowupWithFileAsync(stream, fileName,
            $"{templates.Count} template{(templates.Count == 1 ? "" : "s")}. Edit the file and bring it back with `/template import`; templates with the same name are replaced.",
            ephemeral: true);
    }

    private async Task HandleImportAsync(SocketSlashCommand command, Dictionary<string, object> options)
    {
        if (options.GetValueOrDefault("file") is not IAttachment file)
        {
            await command.FollowupAsync("Attach a .json file.", ephemeral: true);
            return;
        }

        if (!file.Filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && !file.Filename.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            await command.FollowupAsync("That file isn't a .json file.", ephemeral: true);
            return;
        }

        if (file.Size > MaxImportBytes)
        {
            await command.FollowupAsync($"That file is {file.Size / 1024} KB; the most I read is {MaxImportBytes / 1024} KB.", ephemeral: true);
            return;
        }

        string json;
        try
        {
            json = await Http.GetStringAsync(file.Url);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not download import file {File}", file.Filename);
            await command.FollowupAsync("I couldn't download that file from Discord. Try again.", ephemeral: true);
            return;
        }

        var imported = TemplateJson.Import(json, out var readErrors);
        var ownerNames = (await _settings.GetAsync()).OwnerRoles.Select(o => o.Name).ToList();
        var existing = await _templates.GetAllAsync();

        var ready = new List<ProtocolTemplate>();
        var lines = new List<string>();
        var warnings = new List<string>();
        var skipped = 0;
        var namesInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var template in imported)
        {
            var label = string.IsNullOrWhiteSpace(template.Name) ? "(no name)" : template.Name;

            if (!namesInFile.Add(template.Name))
            {
                lines.Add($"⛔ **{label}**: listed twice in the file; only the first one is used.");
                skipped++;
                continue;
            }

            NormalizeOwners(template, ownerNames);
            var check = TemplateRules.Check(template, ownerNames);
            if (!check.IsValid)
            {
                lines.Add($"⛔ **{label}**: skipped. " + string.Join(" ", check.Errors.Take(3)) + (check.Errors.Count > 3 ? $" (+{check.Errors.Count - 3} more)" : ""));
                skipped++;
                continue;
            }

            // Same name (or id) as an existing template: replace it, keeping its id
            var match = existing.FirstOrDefault(e => e.Id == template.Id || string.Equals(e.Name, template.Name, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                template.Id = match.Id;
                template.CreatedAtUtc = match.CreatedAtUtc;
            }

            Stamp(template, command.User);
            ready.Add(template);
            lines.Add(match != null
                ? $"♻️ **{template.Name}**: replaces the current one ({match.Steps.Count} → {template.Steps.Count} steps)"
                : $"✨ **{template.Name}**: new, {template.Steps.Count} steps");
            warnings.AddRange(check.Warnings.Select(w => $"{template.Name}: {w}"));
        }

        var embed = new EmbedBuilder()
            .WithTitle($"Import preview: {file.Filename}")
            .WithColor(ready.Count > 0 ? PanelColor : WarningColor)
            .WithDescription(Truncate(string.Join("\n", readErrors.Select(e => "⛔ " + e).Concat(lines)), 4000));

        if (warnings.Count > 0)
        {
            embed.AddField($"⚠️ To check after importing ({warnings.Count})", Truncate(string.Join("\n", warnings.Take(10).Select(w => "• " + w)), 1024));
        }

        if (ready.Count == 0)
        {
            await command.FollowupAsync("Nothing to import.", embed: embed.Build(), allowedMentions: AllowedMentions.None, ephemeral: true);
            return;
        }

        RemoveExpiredImports();
        var token = Guid.NewGuid().ToString("N")[..10];
        _pendingImports[token] = new PendingImport(command.User.Id, ready, DateTime.UtcNow + PendingImportLifetime);

        var buttons = new ComponentBuilder()
            .WithButton($"Import {ready.Count} template{(ready.Count == 1 ? "" : "s")}", Id("impok", token), ButtonStyle.Success)
            .WithButton("Cancel", Id("impno", token), ButtonStyle.Secondary)
            .Build();

        var note = skipped > 0 ? $"{skipped} template(s) will be skipped. " : string.Empty;
        await command.FollowupAsync(note + "Nothing is saved until you press Import.", embed: embed.Build(), components: buttons,
            allowedMentions: AllowedMentions.None, ephemeral: true);
    }

    private async Task HandleImportButtonAsync(SocketMessageComponent component, string token, bool confirmed)
    {
        if (!_pendingImports.TryRemove(token, out var pending) || pending.ExpiresAtUtc < DateTime.UtcNow)
        {
            await UpdateWithTextAsync(component, "This import expired. Run `/template import` again.");
            return;
        }

        if (pending.UserId != component.User.Id)
        {
            _pendingImports[token] = pending;
            await component.RespondAsync("Only the person who started this import can confirm it.", ephemeral: true);
            return;
        }

        if (!confirmed)
        {
            await UpdateWithTextAsync(component, "Import cancelled. Nothing was changed.");
            return;
        }

        await component.DeferAsync(ephemeral: true);
        var saved = new List<string>();
        foreach (var template in pending.Templates)
        {
            await _templates.SaveAsync(template);
            saved.Add(template.Name);
        }

        _logger.LogInformation("{Count} template(s) imported by {Username}: {Names}", saved.Count, component.User.Username, string.Join(", ", saved));
        await component.ModifyOriginalResponseAsync(p =>
        {
            p.Content = $"Imported {saved.Count}: {string.Join(", ", saved.Select(n => $"**{n}**"))}. Open one with `/template show`.";
            p.Embeds = Array.Empty<Embed>();
            p.Components = new ComponentBuilder().Build();
        });
    }

    private void RemoveExpiredImports()
    {
        foreach (var (token, pending) in _pendingImports)
        {
            if (pending.ExpiresAtUtc < DateTime.UtcNow) _pendingImports.TryRemove(token, out _);
        }
    }

    private sealed record PendingImport(ulong UserId, List<ProtocolTemplate> Templates, DateTime ExpiresAtUtc);

    #endregion

    #region Autocomplete

    private async Task HandleAutocompleteAsync(SocketAutocompleteInteraction interaction)
    {
        var typed = (interaction.Data.Current.Value as string ?? string.Empty).Trim();
        var results = Enumerable.Empty<AutocompleteResult>();

        if (interaction.Data.Current.Name == "template")
        {
            var templates = await _templates.GetAllAsync();
            results = templates
                .Where(t => t.Name.Contains(typed, StringComparison.OrdinalIgnoreCase))
                .Select(t => new AutocompleteResult(TemplateRules.Short(t.Name, 100), t.Id));
        }

        await interaction.RespondAsync(results.Take(25));
    }

    #endregion

    #region Shared helpers

    // The option value is the template id when picked from the suggestions,
    // or whatever was typed (matched against names) otherwise
    private async Task<ProtocolTemplate?> FindTemplateAsync(Dictionary<string, object> options)
    {
        var value = (options.GetValueOrDefault("template") as string ?? string.Empty).Trim();
        if (value.Length == 0) return null;

        return await _templates.GetAsync(value)
            ?? (await _templates.GetAllAsync()).FirstOrDefault(t => string.Equals(t.Name, value, StringComparison.OrdinalIgnoreCase));
    }

    private static string NotFoundText(Dictionary<string, object> options) =>
        $"There's no template called **{options.GetValueOrDefault("template")}**. Pick one from the suggestions, or see `/template list`.";

    // Returns a problem message, or null when the name can be used
    private async Task<string?> CheckNewNameAsync(string name, string? exceptId)
    {
        if (name.Length == 0 || name.Length > ProtocolTemplate.MaxNameLength)
            return $"Template names are 1–{ProtocolTemplate.MaxNameLength} characters.";

        var id = ProtocolTemplate.ToId(name);
        if (id == null)
            return "The name needs at least one letter or number.";

        var all = await _templates.GetAllAsync();
        var clash = all.FirstOrDefault(t => t.Id != exceptId &&
            (string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase) || (exceptId == null && t.Id == id)));
        return clash != null ? $"There's already a template called **{clash.Name}**." : null;
    }

    // "design" -> "Design" when that owner exists, so names stay consistent
    private static void NormalizeOwners(ProtocolTemplate template, IReadOnlyCollection<string> ownerNames)
    {
        foreach (var step in template.Steps)
        {
            step.Owner = NormalizeOwner(step.Owner, ownerNames);
        }
    }

    private static string NormalizeOwner(string owner, IReadOnlyCollection<string> ownerNames) =>
        ownerNames.FirstOrDefault(n => string.Equals(n, owner.Trim(), StringComparison.OrdinalIgnoreCase)) ?? owner.Trim();

    private static void Stamp(ProtocolTemplate template, IUser user)
    {
        template.UpdatedByUserId = user.Id;
        template.UpdatedByUsername = user.Username;
    }

    // Offsets are shown the friendly way in panels and forms
    private static string DescribeOffset(string spec) =>
        DueOffset.TryParse(spec, out var offset, out _) ? offset.Describe() : $"{spec} (not understood)";

    private static string Truncate(string? text, int max)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Length <= max ? text : text[..(max - 1)] + "…";
    }

    #endregion
}
