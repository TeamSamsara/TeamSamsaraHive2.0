using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using TeamSamsara.Hive.Bot.Configuration.Models.DTOs;
using TeamSamsara.Hive.Bot.Configuration.Models.Enums;
using TeamSamsara.Hive.Bot.Configuration.Services;
using TeamSamsara.Hive.Bot.Integrations.Discord;

using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Configuration;

// Orchestrator for Configuration System
// Handles /config slash commands for dynamic Discord configuration management
public class ConfigurationOrchestrator : BaseOrchestrator
{
    private readonly DiscordIntegration _discord;
    
    public override string SystemName => "Configuration";

    #region Constructor
    
    public ConfigurationOrchestrator(
        DiscordIntegration discord,
        ILogger<ConfigurationOrchestrator> logger,
        IServiceProvider serviceProvider)
        : base(logger, serviceProvider)
    {
        _discord = discord ?? throw new ArgumentNullException(nameof(discord));
    }
    
    #endregion

    #region Initialize
    
    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        LogInfo("Initializing Configuration Orchestrator...");
        
        // Register slash commands
        await RegisterSlashCommandsAsync();
        
        // Register event handlers
        _discord.Client.SlashCommandExecuted += HandleSlashCommandAsync;
        
        IsInitialized = true;
        LogInfo("Configuration Orchestrator initialized successfully");
    }
    
    #endregion

    #region Slash Command Registration
    
    private async Task RegisterSlashCommandsAsync()
    {
        var guild = _discord.Client.Guilds.FirstOrDefault();
        if (guild == null)
        {
            LogWarning("No guild found for slash command registration");
            return;
        }
        
        // Main /config command with subcommands
        var configCommand = new SlashCommandBuilder()
            .WithName("config")
            .WithDescription("Manage bot configuration")
            .WithDefaultMemberPermissions(GuildPermission.Administrator)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("set-stream-channel")
                .WithDescription("Set stream notification channel")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("channel", ApplicationCommandOptionType.Channel, "Channel to use", isRequired: true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("set-urgent-ticket-channel")
                .WithDescription("Set urgent ticket notification channel")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("channel", ApplicationCommandOptionType.Channel, "Channel to use", isRequired: true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("set-admin-channel")
                .WithDescription("Set admin alerts channel")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("channel", ApplicationCommandOptionType.Channel, "Channel to use", isRequired: true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("set-analytics-channel")
                .WithDescription("Set analytics report channel")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("channel", ApplicationCommandOptionType.Channel, "Channel to use", isRequired: true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("set-ticket-log-channel")
                .WithDescription("Set ticket log channel")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("channel", ApplicationCommandOptionType.Channel, "Channel to use", isRequired: true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("set-admin-role")
                .WithDescription("Set admin role")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("role", ApplicationCommandOptionType.Role, "Role to use", isRequired: true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("set-moderator-role")
                .WithDescription("Set moderator role")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("role", ApplicationCommandOptionType.Role, "Role to use", isRequired: true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("set-staff-role")
                .WithDescription("Set staff role")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("role", ApplicationCommandOptionType.Role, "Role to use", isRequired: true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("set-stream-role")
                .WithDescription("Set stream notification role")
                .WithType(ApplicationCommandOptionType.SubCommand)
                .AddOption("role", ApplicationCommandOptionType.Role, "Role to use", isRequired: true))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("show")
                .WithDescription("Show current configuration")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("validate")
                .WithDescription("Validate all configured IDs")
                .WithType(ApplicationCommandOptionType.SubCommand))
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("export")
                .WithDescription("Export configuration to JSON")
                .WithType(ApplicationCommandOptionType.SubCommand));
        
        await guild.CreateApplicationCommandAsync(configCommand.Build());
        LogInfo("Registered /config command with subcommands");
    }
    
    #endregion

    #region Slash Command Handlers
    
    private async Task HandleSlashCommandAsync(SocketSlashCommand command)
    {
        if (command.CommandName != "config")
            return;
        
        try
        {
            await command.DeferAsync(ephemeral: true);
            
            var subCommand = command.Data.Options.FirstOrDefault()?.Name;
            
            switch (subCommand)
            {
                case "set-stream-channel":
                    await HandleSetChannelAsync(command, ConfigKey.StreamNotificationChannelId);
                    break;
                case "set-urgent-ticket-channel":
                    await HandleSetChannelAsync(command, ConfigKey.UrgentTicketChannelId);
                    break;
                case "set-admin-channel":
                    await HandleSetChannelAsync(command, ConfigKey.AdminChannelId);
                    break;
                case "set-analytics-channel":
                    await HandleSetChannelAsync(command, ConfigKey.AnalyticsReportChannelId);
                    break;
                case "set-ticket-log-channel":
                    await HandleSetChannelAsync(command, ConfigKey.TicketLogChannelId);
                    break;
                case "set-admin-role":
                    await HandleSetRoleAsync(command, ConfigKey.AdminRoleId);
                    break;
                case "set-moderator-role":
                    await HandleSetRoleAsync(command, ConfigKey.ModeratorRoleId);
                    break;
                case "set-staff-role":
                    await HandleSetRoleAsync(command, ConfigKey.StaffRoleId);
                    break;
                case "set-stream-role":
                    await HandleSetRoleAsync(command, ConfigKey.StreamNotificationRoleId);
                    break;
                case "show":
                    await HandleShowConfigAsync(command);
                    break;
                case "validate":
                    await HandleValidateAsync(command);
                    break;
                case "export":
                    await HandleExportAsync(command);
                    break;
                default:
                    await command.FollowupAsync("Unknown subcommand", ephemeral: true);
                    break;
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Error handling /config command");
            await command.FollowupAsync("An error occurred while processing your command", ephemeral: true);
        }
    }
    
    private async Task HandleSetChannelAsync(SocketSlashCommand command, ConfigKey key)
    {
        // Create a scope to resolve scoped services
        using var scope = ServiceProvider.CreateScope();
        var setConfigService = scope.ServiceProvider.GetRequiredService<SetConfigValueService>();
        
        var channelOption = command.Data.Options.FirstOrDefault()?.Options.FirstOrDefault(o => o.Name == "channel");
        if (channelOption?.Value is not IChannel channel)
        {
            await command.FollowupAsync("Invalid channel specified", ephemeral: true);
            return;
        }
        
        var channelId = channel.Id;
        var displayName = channel.Name;
        
        var dto = new SetConfigDto
        {
            Key = key,
            ValueId = channelId,
            DisplayName = $"#{displayName}",
            UpdatedByUserId = command.User.Id,
            UpdatedByUsername = command.User.Username
        };
        
        await setConfigService.SetChannelAsync(dto);
        
        var embed = new EmbedBuilder()
            .WithTitle("Configuration Updated")
            .WithDescription($"**{key}** has been set")
            .AddField("Channel", $"<#{channelId}>", inline: true)
            .AddField("Updated By", command.User.Mention, inline: true)
            .WithColor(Color.Green)
            .WithCurrentTimestamp()
            .Build();
        
        await command.FollowupAsync(embed: embed, ephemeral: true);
    }
    
    private async Task HandleSetRoleAsync(SocketSlashCommand command, ConfigKey key)
    {
        // Create a scope to resolve scoped services
        using var scope = ServiceProvider.CreateScope();
        var setConfigService = scope.ServiceProvider.GetRequiredService<SetConfigValueService>();
        
        var roleOption = command.Data.Options.FirstOrDefault()?.Options.FirstOrDefault(o => o.Name == "role");
        if (roleOption?.Value is not IRole role)
        {
            await command.FollowupAsync("Invalid role specified", ephemeral: true);
            return;
        }
        
        var roleId = role.Id;
        var displayName = role.Name;
        
        var dto = new SetConfigDto
        {
            Key = key,
            ValueId = roleId,
            DisplayName = $"@{displayName}",
            UpdatedByUserId = command.User.Id,
            UpdatedByUsername = command.User.Username
        };
        
        await setConfigService.SetRoleAsync(dto);
        
        var embed = new EmbedBuilder()
            .WithTitle("Configuration Updated")
            .WithDescription($"**{key}** has been set")
            .AddField("Role", role.Mention, inline: true)
            .AddField("Updated By", command.User.Mention, inline: true)
            .WithColor(Color.Green)
            .WithCurrentTimestamp()
            .Build();
        
        await command.FollowupAsync(embed: embed, ephemeral: true);
    }
    
    private async Task HandleShowConfigAsync(SocketSlashCommand command)
    {
        // Create a scope to resolve scoped services
        using var scope = ServiceProvider.CreateScope();
        var getConfigService = scope.ServiceProvider.GetRequiredService<GetConfigValueService>();
        
        var configs = await getConfigService.GetAllAsync();
        
        if (configs.Count == 0)
        {
            await command.FollowupAsync("No configuration set", ephemeral: true);
            return;
        }
        
        var embed = new EmbedBuilder()
            .WithTitle("Bot Configuration")
            .WithDescription($"Showing {configs.Count} configuration entries")
            .WithColor(Color.Blue)
            .WithCurrentTimestamp();
        
        foreach (var config in configs.OrderBy(c => c.Key))
        {
            embed.AddField(config.Key, config.DisplayName ?? config.Value, inline: true);
        }
        
        await command.FollowupAsync(embed: embed.Build(), ephemeral: true);
    }
    
    private async Task HandleValidateAsync(SocketSlashCommand command)
    {
        // Create a scope to resolve scoped services
        using var scope = ServiceProvider.CreateScope();
        var validateService = scope.ServiceProvider.GetRequiredService<ValidateConfigService>();
        
        var result = await validateService.ValidateAllConfigurationsAsync();
        
        var embed = new EmbedBuilder()
            .WithTitle("Configuration Validation")
            .WithDescription($"Validated {result.TotalConfigurations} entries")
            .AddField("Valid", result.ValidConfigurations, inline: true)
            .AddField("Invalid", result.InvalidConfigurations.Count, inline: true)
            .WithColor(result.AllValid ? Color.Green : Color.Orange)
            .WithCurrentTimestamp();
        
        if (!result.AllValid)
        {
            foreach (var invalid in result.InvalidConfigurations)
            {
                embed.AddField($"{invalid.Key}", $"**Issue:** {invalid.Reason}\n**Last Updated:** {invalid.LastUpdated:yyyy-MM-dd} by {invalid.LastUpdatedBy}", inline: false);
            }
        }
        
        await command.FollowupAsync(embed: embed.Build(), ephemeral: true);
    }
    
    private async Task HandleExportAsync(SocketSlashCommand command)
    {
        // Create a scope to resolve scoped services
        using var scope = ServiceProvider.CreateScope();
        var exportService = scope.ServiceProvider.GetRequiredService<ExportConfigService>();
        
        var json = await exportService.ExportToJsonAsync(command.User.Id, command.User.Username);
        
        var fileName = $"hive-config-{DateTime.UtcNow:yyyy-MM-dd}.json";
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        
        await command.FollowupWithFileAsync(stream, fileName, "Configuration exported", ephemeral: true);
    }
    
    #endregion
}