using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Integrations.Discord;
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
        _discord.Client.SlashCommandExecuted += OnSlashCommandExecutedAsync;
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

        var addSubCommand = new SlashCommandOptionBuilder()
            .WithName("add")
            .WithDescription("Start tracking a Twitch stream for live notifications")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption(addOption);

        var streamCommand = new SlashCommandBuilder()
            .WithName("stream")
            .WithDescription("Manage tracked Twitch streams")
            .AddOption(addSubCommand);

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

            using var scope = _serviceProvider.CreateScope();
            var addStreamService = scope.ServiceProvider.GetRequiredService<AddStreamService>();

            var result = await addStreamService.AddStreamAsync(input, command.User.Id, command.User.Username);

            await command.FollowupAsync(result.Message);
        }
    }
}