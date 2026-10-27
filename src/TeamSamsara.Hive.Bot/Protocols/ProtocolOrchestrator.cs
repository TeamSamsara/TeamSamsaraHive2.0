using System.Globalization;
using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Integrations.Discord;
using TeamSamsara.Hive.Bot.Protocols.Models;
using TeamSamsara.Hive.Bot.Protocols.Repositories;
using TeamSamsara.Hive.Bot.Protocols.Templates;
using TeamSamsara.Hive.Core.Abstractions;
using TeamSamsara.Hive.Core.Utilities;

namespace TeamSamsara.Hive.Bot.Protocols;

// Root orchestrator for the protocols feature (checklists, planning, reminders).
//
//   /protocol setup     category, calendar channel, digest channel, time zone, planner role
//   /protocol settings  show the current setup
//   /protocol role set | remove | list   owner names ("Design") -> Discord roles
//   /template ...       checklist templates (TemplateCommandHandler)
public class ProtocolOrchestrator : BaseOrchestrator
{
    private const string CommandName = "protocol";
    private static readonly Regex OwnerNamePattern = new(@"^[\p{L}\p{N}][\p{L}\p{N} _\-]{0,31}$", RegexOptions.Compiled);

    // Suggestions for the time zone option; any valid IANA name is accepted
    private static readonly string[] CommonTimeZones =
    {
        "America/Toronto", "America/New_York", "America/Halifax", "America/St_Johns", "America/Winnipeg",
        "America/Chicago", "America/Edmonton", "America/Denver", "America/Vancouver", "America/Los_Angeles",
        "America/Mexico_City", "America/Sao_Paulo", "UTC", "Europe/London", "Europe/Paris",
        "Europe/Berlin", "Europe/Madrid", "Europe/Stockholm", "Europe/Helsinki", "Europe/Moscow",
        "Asia/Seoul", "Asia/Tokyo", "Asia/Shanghai", "Asia/Singapore", "Australia/Sydney"
    };

    private readonly DiscordIntegration _discord;
    private readonly IProtocolSettingsRepository _settings;
    private readonly ITemplateRepository _templates;
    private readonly TemplateCommandHandler _templateCommands;

    public override string SystemName => "Protocols";

    public ProtocolOrchestrator(
        DiscordIntegration discord,
        IProtocolSettingsRepository settings,
        ITemplateRepository templates,
        TemplateCommandHandler templateCommands,
        ILogger<ProtocolOrchestrator> logger,
        IServiceProvider serviceProvider)
        : base(logger, serviceProvider)
    {
        _discord = discord ?? throw new ArgumentNullException(nameof(discord));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _templates = templates ?? throw new ArgumentNullException(nameof(templates));
        _templateCommands = templateCommands ?? throw new ArgumentNullException(nameof(templateCommands));
    }

    #region Lifecycle

    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await base.InitializeAsync(cancellationToken);

        _discord.Interactions.OnSlashCommand(CommandName, HandleSlashCommandAsync);
        _discord.Interactions.OnAutocomplete(CommandName, HandleAutocompleteAsync);

        await RegisterCommandAsync();
        await _templateCommands.RegisterAsync();

        // Load settings now so a broken database shows up at startup
        var settings = await _settings.GetAsync();
        if (!ZonedTime.TryResolve(settings.TimeZoneId, out _))
        {
            LogWarning("Saved protocol time zone {TimeZone} is not recognised on this machine; times will use UTC until it is fixed with /protocol setup",
                settings.TimeZoneId);
        }

        var templates = await _templates.GetAllAsync();
        LogInfo("Protocols initialized ({Count} template(s))", templates.Count);
    }

    private async Task RegisterCommandAsync()
    {
        var guild = _discord.Client.Guilds.FirstOrDefault();
        if (guild == null)
        {
            LogWarning("No guild found for /protocol registration");
            return;
        }

        var command = new SlashCommandBuilder()
            .WithName(CommandName)
            .WithDescription("Social checklists: setup and owner roles (templates live in /template)")
            .WithDefaultMemberPermissions(GuildPermission.Administrator)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("setup")
                .WithDescription("Choose the category, channels, time zone and planner role")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("category").WithDescription("Category that holds the protocol channels")
                    .WithType(ApplicationCommandOptionType.Channel).AddChannelType(ChannelType.Category).WithRequired(false))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("calendar-channel").WithDescription("Channel for the pinned calendar")
                    .WithType(ApplicationCommandOptionType.Channel).AddChannelType(ChannelType.Text).WithRequired(false))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("digest-channel").WithDescription("Channel for the Monday digest and planning checks")
                    .WithType(ApplicationCommandOptionType.Channel).AddChannelType(ChannelType.Text).WithRequired(false))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("timezone").WithDescription("Time zone for times you type, e.g. America/Toronto")
                    .WithType(ApplicationCommandOptionType.String).WithAutocomplete(true).WithRequired(false))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("planner-role").WithDescription("Role that gets the plan-ahead reminders")
                    .WithType(ApplicationCommandOptionType.Role).WithRequired(false)))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("settings")
                .WithDescription("Show the current protocol setup")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("role")
                .WithDescription("Owner names used in templates, mapped to Discord roles")
                .WithType(ApplicationCommandOptionType.SubCommandGroup)
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("set").WithDescription("Map an owner name (e.g. Design) to a Discord role")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption(new SlashCommandOptionBuilder()
                        .WithName("name").WithDescription("Owner name used in templates, e.g. Design")
                        .WithType(ApplicationCommandOptionType.String).WithAutocomplete(true).WithRequired(true))
                    .AddOption(new SlashCommandOptionBuilder()
                        .WithName("role").WithDescription("Discord role for this owner")
                        .WithType(ApplicationCommandOptionType.Role).WithRequired(true)))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("remove").WithDescription("Remove an owner name")
                    .WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption(new SlashCommandOptionBuilder()
                        .WithName("name").WithDescription("Owner name to remove")
                        .WithType(ApplicationCommandOptionType.String).WithAutocomplete(true).WithRequired(true)))
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("list").WithDescription("List owner names and their roles")
                    .WithType(ApplicationCommandOptionType.SubCommand)));

        try
        {
            await guild.CreateApplicationCommandAsync(command.Build());
            LogInfo("Registered /protocol command");
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to register /protocol command");
        }
    }

    #endregion

    #region Slash command

    private async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        await command.DeferAsync(ephemeral: true);

        var sub = command.Data.Options.FirstOrDefault();
        switch (sub?.Name)
        {
            case "setup":
                await HandleSetupAsync(command, sub);
                break;
            case "settings":
                await command.FollowupAsync(embed: BuildSettingsEmbed(await _settings.GetAsync(), "Protocol setup"), ephemeral: true);
                break;
            case "role":
                var action = sub.Options.FirstOrDefault();
                switch (action?.Name)
                {
                    case "set":
                        await HandleRoleSetAsync(command, action);
                        break;
                    case "remove":
                        await HandleRoleRemoveAsync(command, action);
                        break;
                    default:
                        await command.FollowupAsync(embed: BuildSettingsEmbed(await _settings.GetAsync(), "Owner roles", ownersOnly: true), ephemeral: true);
                        break;
                }
                break;
            default:
                await command.FollowupAsync("Unknown subcommand", ephemeral: true);
                break;
        }
    }

    private async Task HandleSetupAsync(SocketSlashCommand command, SocketSlashCommandDataOption sub)
    {
        var options = sub.Options.ToDictionary(o => o.Name, o => o.Value);
        var settings = await _settings.GetAsync();

        if (options.Count == 0)
        {
            await command.FollowupAsync(embed: BuildSettingsEmbed(settings, "Protocol setup"), ephemeral: true);
            return;
        }

        var problems = new List<string>();
        var notes = new List<string>();

        if (options.TryGetValue("category", out var categoryValue) && categoryValue is ICategoryChannel category)
        {
            settings.CategoryId = category.Id;
        }

        if (options.TryGetValue("timezone", out var tzValue) && tzValue is string tzText)
        {
            if (ZonedTime.TryResolve(tzText, out var zone))
            {
                settings.TimeZoneId = zone.Id;
            }
            else
            {
                problems.Add($"`{tzText}` isn't a time zone I know. Use a name like `America/Toronto` (pick from the suggestions).");
            }
        }

        if (options.TryGetValue("planner-role", out var plannerValue) && plannerValue is IRole plannerRole)
        {
            if (plannerRole.Id == plannerRole.Guild.EveryoneRole.Id)
                problems.Add("The planner role can't be @everyone.");
            else
                settings.PlannerRoleId = plannerRole.Id;
        }

        SetChannel(options, "calendar-channel", settings, problems, id => settings.CalendarChannelId = id);
        SetChannel(options, "digest-channel", settings, problems, id => settings.DigestChannelId = id);

        // A category change can leave earlier channels outside it
        if (settings.CategoryId != null)
        {
            foreach (var (label, channelId) in new[] { ("calendar", settings.CalendarChannelId), ("digest", settings.DigestChannelId) })
            {
                if (channelId != null && _discord.Client.GetChannel(channelId.Value) is INestedChannel nested && nested.CategoryId != settings.CategoryId)
                {
                    notes.Add($"The {label} channel <#{channelId}> isn't in the protocol category. Move it there, or pick another one.");
                }
            }
        }

        if (problems.Count > 0)
        {
            await command.FollowupAsync("Nothing was saved:\n• " + string.Join("\n• ", problems), ephemeral: true);
            return;
        }

        StampUpdate(settings, command.User);
        await _settings.SaveAsync(settings);
        LogInfo("Protocol setup updated by {Username}", command.User.Username);

        var text = notes.Count > 0 ? "Saved, with a heads-up:\n• " + string.Join("\n• ", notes) : "Saved.";
        await command.FollowupAsync(text, embed: BuildSettingsEmbed(settings, "Protocol setup"), ephemeral: true);
    }

    // Channels must be text channels, inside the protocol category when one is set
    private static void SetChannel(
        Dictionary<string, object> options, string optionName, ProtocolSettings settings, List<string> problems, Action<ulong> apply)
    {
        if (!options.TryGetValue(optionName, out var value) || value is not ITextChannel channel)
        {
            return;
        }

        if (channel is IThreadChannel)
        {
            problems.Add($"<#{channel.Id}> is a thread; pick a regular channel.");
            return;
        }

        if (settings.CategoryId != null && channel.CategoryId != settings.CategoryId)
        {
            problems.Add($"<#{channel.Id}> isn't in the protocol category <#{settings.CategoryId}>. Move it there first, or pick a channel inside it.");
            return;
        }

        apply(channel.Id);
    }

    private async Task HandleRoleSetAsync(SocketSlashCommand command, SocketSlashCommandDataOption action)
    {
        var name = (action.Options.FirstOrDefault(o => o.Name == "name")?.Value as string ?? string.Empty).Trim();
        var role = action.Options.FirstOrDefault(o => o.Name == "role")?.Value as IRole;

        if (!OwnerNamePattern.IsMatch(name))
        {
            await command.FollowupAsync("Owner names are 1–32 characters: letters, numbers, spaces, `-` or `_`, e.g. `Design`.", ephemeral: true);
            return;
        }

        if (role == null || role.Id == role.Guild.EveryoneRole.Id)
        {
            await command.FollowupAsync("Pick a real role (not @everyone).", ephemeral: true);
            return;
        }

        var settings = await _settings.GetAsync();
        var existing = settings.FindOwner(name);
        var previousRoleId = existing?.RoleId;
        if (existing != null)
        {
            existing.RoleId = role.Id;
        }
        else
        {
            settings.OwnerRoles.Add(new OwnerRole { Name = name, RoleId = role.Id });
            settings.OwnerRoles = settings.OwnerRoles.OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        StampUpdate(settings, command.User);
        await _settings.SaveAsync(settings);
        LogInfo("Owner {Name} set to role {RoleId} by {Username}", name, role.Id, command.User.Username);

        var message = previousRoleId != null && previousRoleId != role.Id
            ? $"**{existing!.Name}** now points to <@&{role.Id}> (was <@&{previousRoleId}>). Templates using **{existing.Name}** follow automatically."
            : $"**{existing?.Name ?? name}** → <@&{role.Id}>. Use **{existing?.Name ?? name}** as an owner in templates.";
        await command.FollowupAsync(message, allowedMentions: AllowedMentions.None, ephemeral: true);
    }

    private async Task HandleRoleRemoveAsync(SocketSlashCommand command, SocketSlashCommandDataOption action)
    {
        var name = (action.Options.FirstOrDefault(o => o.Name == "name")?.Value as string ?? string.Empty).Trim();
        var settings = await _settings.GetAsync();
        var existing = settings.FindOwner(name);

        if (existing == null)
        {
            await command.FollowupAsync($"There's no owner called **{name}**. See `/protocol role list`.", ephemeral: true);
            return;
        }

        // Templates refer to owners by name, so removing one still in use would orphan those steps
        var usedBy = (await _templates.GetAllAsync())
            .Select(t => (t.Name, Count: t.Steps.Count(s => string.Equals(s.Owner, existing.Name, StringComparison.OrdinalIgnoreCase))))
            .Where(x => x.Count > 0)
            .ToList();
        if (usedBy.Count > 0)
        {
            await command.FollowupAsync(
                $"**{existing.Name}** is still the owner of steps in: {string.Join(", ", usedBy.Select(u => $"**{u.Name}** ({u.Count})"))}.\n" +
                $"Give those steps another owner first, or point **{existing.Name}** at a different role with `/protocol role set`.",
                ephemeral: true);
            return;
        }

        settings.OwnerRoles.Remove(existing);
        StampUpdate(settings, command.User);
        await _settings.SaveAsync(settings);
        LogInfo("Owner {Name} removed by {Username}", existing.Name, command.User.Username);

        await command.FollowupAsync($"Removed **{existing.Name}**.", ephemeral: true);
    }

    private static void StampUpdate(ProtocolSettings settings, IUser user)
    {
        settings.UpdatedByUserId = user.Id;
        settings.UpdatedByUsername = user.Username;
    }

    #endregion

    #region Autocomplete

    private async Task HandleAutocompleteAsync(SocketAutocompleteInteraction interaction)
    {
        var focused = interaction.Data.Current;
        var typed = (focused.Value as string ?? string.Empty).Trim();
        IEnumerable<string> suggestions;

        if (focused.Name == "timezone")
        {
            var matches = CommonTimeZones.Where(z => z.Contains(typed, StringComparison.OrdinalIgnoreCase)).ToList();
            if (typed.Length > 0 && !matches.Contains(typed, StringComparer.OrdinalIgnoreCase) && ZonedTime.TryResolve(typed, out var zone))
            {
                matches.Insert(0, zone.Id); // a valid zone that isn't in the short list
            }
            suggestions = matches;
        }
        else if (focused.Name == "name")
        {
            var settings = await _settings.GetAsync();
            suggestions = settings.OwnerRoles
                .Select(o => o.Name)
                .Where(n => n.Contains(typed, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            suggestions = Enumerable.Empty<string>();
        }

        await interaction.RespondAsync(suggestions.Take(25).Select(s => new AutocompleteResult(s, s)));
    }

    #endregion

    #region Display

    private Embed BuildSettingsEmbed(ProtocolSettings s, string title, bool ownersOnly = false)
    {
        var embed = new EmbedBuilder().WithTitle(title).WithColor(new Color(0xB8F24A));

        if (!ownersOnly)
        {
            var zoneKnown = ZonedTime.TryResolve(s.TimeZoneId, out var zone);
            var zoneText = zoneKnown
                ? $"{zone.Id} (it's {ZonedTime.ToLocal(DateTime.UtcNow, zone).ToString("h:mm tt", CultureInfo.InvariantCulture)} there now)"
                : $"{s.TimeZoneId} (not recognised, fix with /protocol setup)";

            embed.AddField("Category", s.CategoryId is { } c ? $"<#{c}>" : "Not set", inline: true)
                 .AddField("Calendar channel", s.CalendarChannelId is { } cal ? $"<#{cal}>" : "Not set", inline: true)
                 .AddField("Digest channel", s.DigestChannelId is { } dig ? $"<#{dig}>" : "Not set", inline: true)
                 .AddField("Planner role", s.PlannerRoleId is { } pr ? $"<@&{pr}>" : "Not set", inline: true)
                 .AddField("Time zone", zoneText, inline: true);
        }

        var owners = s.OwnerRoles.Count == 0
            ? "None yet. Add one with `/protocol role set name: Design role: @YourRole`."
            : string.Join("\n", s.OwnerRoles.Select(o => $"**{o.Name}** → <@&{o.RoleId}>"));
        embed.AddField($"Owner roles ({s.OwnerRoles.Count})", owners);

        if (s.UpdatedAtUtc is { } updated)
        {
            embed.AddField("Last change", $"{ZonedTime.DiscordTimestamp(updated, 'R')} by {s.UpdatedByUsername ?? "unknown"}");
        }

        if (!ownersOnly)
        {
            var missing = new List<string>();
            if (s.CategoryId == null) missing.Add("category");
            if (s.CalendarChannelId == null) missing.Add("calendar channel");
            if (s.DigestChannelId == null) missing.Add("digest channel");
            if (s.PlannerRoleId == null) missing.Add("planner role");
            if (missing.Count > 0)
            {
                embed.WithDescription("Still to set: " + string.Join(", ", missing) + ". Use `/protocol setup`.");
            }
        }

        return embed.Build();
    }

    #endregion
}
