using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Integrations.Discord;
using TeamSamsara.Hive.Bot.Notifications;
using TeamSamsara.Hive.Bot.Streams.Services;

namespace TeamSamsara.Hive.Bot.Streams.Commands;

// Registers and handles the /stream slash command family
public class StreamCommandHandler
{
    private readonly DiscordIntegration _discord;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StreamCommandHandler> _logger;

    public StreamCommandHandler(
        DiscordIntegration discord,
        IServiceProvider serviceProvider,
        ILogger<StreamCommandHandler> logger)
    {
        _discord = discord;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    // Call once after Discord is connected (see wiring notes)
    public void RegisterEventHandlers()
    {
        _discord.Interactions.OnSlashCommand("stream", OnSlashCommandExecutedAsync);
    }

    // Registers /stream with Discord - safe to call on every startup,
    // Discord no-ops if the command definition hasn't changed
    public async Task RegisterCommandsAsync()
    {
        var addOption = new SlashCommandOptionBuilder()
            .WithName("username")
            .WithDescription("Twitch username or channel link")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true);

        var addRoleOption = new SlashCommandOptionBuilder()
            .WithName("role")
            .WithDescription("Role to ping for this streamer instead of the default (optional)")
            .WithType(ApplicationCommandOptionType.Role)
            .WithRequired(false);

        var addSubCommand = new SlashCommandOptionBuilder()
            .WithName("add")
            .WithDescription("Start tracking a Twitch stream for live notifications")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(addOption)
            .AddOption(addRoleOption);

        var setRoleUsernameOption = new SlashCommandOptionBuilder()
            .WithName("username")
            .WithDescription("Twitch username of an already-tracked streamer")
            .WithType(ApplicationCommandOptionType.String)
            .WithRequired(true);

        var setRoleRoleOption = new SlashCommandOptionBuilder()
            .WithName("role")
            .WithDescription("Role to ping when this streamer goes live")
            .WithType(ApplicationCommandOptionType.Role)
            .WithRequired(true);

        var setRoleSubCommand = new SlashCommandOptionBuilder()
            .WithName("set-role")
            .WithDescription("Set (or change) which role gets pinged for a specific tracked streamer")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(setRoleUsernameOption)
            .AddOption(setRoleRoleOption);

        var listSubCommand = new SlashCommandOptionBuilder()
            .WithName("list")
            .WithDescription("List every streamer currently being tracked")
            .WithType(ApplicationCommandOptionType.SubCommand);

        var streamCommand = new SlashCommandBuilder()
            .WithName("stream")
            .WithDescription("Manage tracked Twitch streams")
            .AddOption(addSubCommand)
            .AddOption(setRoleSubCommand)
            .AddOption(listSubCommand);

        try
        {
            await _discord.Client.CreateGlobalApplicationCommandAsync(streamCommand.Build());
            _logger.LogInformation("[StreamCommandHandler] /stream command registered");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StreamCommandHandler] Failed to register /stream command");
        }
    }

    private async Task OnSlashCommandExecutedAsync(SocketSlashCommand command)
    {
        if (command.CommandName != "stream") return;

        var subCommand = command.Data.Options.FirstOrDefault();
        if (subCommand == null) return;

        if (subCommand.Name == "add")
        {
            await command.DeferAsync();

            var input = subCommand.Options.FirstOrDefault(o => o.Name == "username")?.Value?.ToString() ?? string.Empty;
            var roleOption = subCommand.Options.FirstOrDefault(o => o.Name == "role");
            var roleId = roleOption?.Value is IRole role ? role.Id : (ulong?)null;

            using var scope = _serviceProvider.CreateScope();
            var addStreamService = scope.ServiceProvider.GetRequiredService<AddStreamService>();

            var result = await addStreamService.AddStreamAsync(input, command.User.Id, command.User.Username, roleId);

            await command.FollowupAsync(result.Message);
        }
        else if (subCommand.Name == "set-role")
        {
            await command.DeferAsync();

            var input = subCommand.Options.FirstOrDefault(o => o.Name == "username")?.Value?.ToString() ?? string.Empty;
            var roleOption = subCommand.Options.FirstOrDefault(o => o.Name == "role");

            if (roleOption?.Value is not IRole role)
            {
                await command.FollowupAsync("Invalid role provided.");
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var addStreamService = scope.ServiceProvider.GetRequiredService<AddStreamService>();

            var result = await addStreamService.SetStreamRoleAsync(input, role.Id);

            await command.FollowupAsync(result.Message);
        }
        else if (subCommand.Name == "list")
        {
            await command.DeferAsync();

            using var scope = _serviceProvider.CreateScope();
            var addStreamService = scope.ServiceProvider.GetRequiredService<AddStreamService>();

            var streams = await addStreamService.ListStreamsAsync();
            var embed = NotificationTemplates.BuildStreamListEmbed(streams);

            await command.FollowupAsync(embed: embed);
        }
    }
}