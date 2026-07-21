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

public class ConfigurationOrchestrator : BaseOrchestrator
{
    private readonly DiscordIntegration _discord;
    
    public override string SystemName => "Configuration";

    public ConfigurationOrchestrator(
        DiscordIntegration discord,
        ILogger<ConfigurationOrchestrator> logger,
        IServiceProvider serviceProvider)
        : base(logger, serviceProvider)
    {
        _discord = discord ?? throw new ArgumentNullException(nameof(discord));
    }
    
    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        LogInfo("Initializing Configuration Orchestrator...");
        
        await RegisterSlashCommandsAsync();
        
        _discord.Client.SlashCommandExecuted += HandleSlashCommandAsync;
        
        IsInitialized = true;
        LogInfo("Configuration Orchestrator initialized successfully");
    }
    
    private async Task RegisterSlashCommandsAsync()
    {
        var guild = _discord.Client.Guilds.FirstOrDefault();
        if (guild == null)
        {
            LogWarning("No guild found for slash command registration");
            return;
        }
        
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
                .WithName("set-ticket-embed-channel")
                .WithDescription("Set ticket creation embed channel")
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
                case "set-ticket-embed-channel":
                    await HandleSetChannelAsync(command, ConfigKey.TicketEmbedChannelId);
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
        using var scope = ServiceProvider.CreateScope();
        var setConfigService = scope.ServiceProvider.GetRequiredService<SetConfigValueService>();
        
        var channelOption = command.Data.Options.First().Options.FirstOrDefault(x => x.Name == "channel");
        if (channelOption?.Value is not IChannel channel)
        {
            await command.FollowupAsync("Invalid channel provided", ephemeral: true);
            return;
        }
        
        var dto = new SetConfigDto
        {
            Key = key,
            ValueId = channel.Id,
            DisplayName = channel.Name,
            UpdatedByUserId = command.User.Id,
            UpdatedByUsername = command.User.Username
        };
        
        await setConfigService.SetChannelAsync(dto);
        
        await command.FollowupAsync(
            $"✅ Configuration Updated\n" +
            $"**{key}** has been set\n" +
            $"Channel: <#{channel.Id}>",
            ephemeral: true);
        
        LogInfo("Updated {ConfigKey} to channel {ChannelId} by {Username}", key, channel.Id, command.User.Username);
    }
    
    private async Task HandleSetRoleAsync(SocketSlashCommand command, ConfigKey key)
    {
        using var scope = ServiceProvider.CreateScope();
        var setConfigService = scope.ServiceProvider.GetRequiredService<SetConfigValueService>();
        
        var roleOption = command.Data.Options.First().Options.FirstOrDefault(x => x.Name == "role");
        if (roleOption?.Value is not IRole role)
        {
            await command.FollowupAsync("Invalid role provided", ephemeral: true);
            return;
        }
        
        var dto = new SetConfigDto
        {
            Key = key,
            ValueId = role.Id,
            DisplayName = role.Name,
            UpdatedByUserId = command.User.Id,
            UpdatedByUsername = command.User.Username
        };
        
        await setConfigService.SetRoleAsync(dto);
        
        await command.FollowupAsync(
            $"✅ Configuration Updated\n" +
            $"**{key}** has been set\n" +
            $"Role: <@&{role.Id}>",
            ephemeral: true);
        
        LogInfo("Updated {ConfigKey} to role {RoleId} by {Username}", key, role.Id, command.User.Username);
    }
    
    private async Task HandleShowConfigAsync(SocketSlashCommand command)
    {
        using var scope = ServiceProvider.CreateScope();
        var exportService = scope.ServiceProvider.GetRequiredService<ExportConfigService>();
        
        var json = await exportService.ExportToJsonAsync(command.User.Id, command.User.Username);
        var export = System.Text.Json.JsonSerializer.Deserialize<ExportConfigDto>(json);
        
        if (export == null || export.Configurations.Count == 0)
        {
            await command.FollowupAsync("No configuration entries found", ephemeral: true);
            return;
        }
        
        var embed = new EmbedBuilder()
            .WithTitle("Bot Configuration")
            .WithDescription($"Showing {export.Configurations.Count} configuration entries")
            .WithColor(Color.Blue);
        
        foreach (var config in export.Configurations.Take(25))
        {
            embed.AddField(config.Key, config.Value ?? "Not set", inline: true);
        }
        
        await command.FollowupAsync(embed: embed.Build(), ephemeral: true);
        
        LogInfo("Displayed configuration to {Username}", command.User.Username);
    }
    
    private async Task HandleValidateAsync(SocketSlashCommand command)
    {
        using var scope = ServiceProvider.CreateScope();
        var validateService = scope.ServiceProvider.GetRequiredService<ValidateConfigService>();
        
        var result = await validateService.ValidateAllConfigurationsAsync();
        
        var embed = new EmbedBuilder()
            .WithTitle("Configuration Validation")
            .WithColor(result.IsValid ? Color.Green : Color.Red);
        
        if (result.IsValid)
        {
            embed.WithDescription("✅ All configuration values are valid");
        }
        else
        {
            embed.WithDescription($"❌ Found {result.InvalidConfigurations.Count} invalid configuration(s)");
            
            foreach (var invalid in result.InvalidConfigurations.Take(10))
            {
                embed.AddField(
                    invalid.Key,
                    $"**Reason:** {invalid.Reason}\n**Last Updated:** {invalid.LastUpdated:yyyy-MM-dd HH:mm} by {invalid.LastUpdatedBy}",
                    inline: false);
            }
        }
        
        await command.FollowupAsync(embed: embed.Build(), ephemeral: true);
        
        LogInfo("Validated configuration for {Username}", command.User.Username);
    }
    
    private async Task HandleExportAsync(SocketSlashCommand command)
    {
        using var scope = ServiceProvider.CreateScope();
        var exportService = scope.ServiceProvider.GetRequiredService<ExportConfigService>();
        
        var json = await exportService.ExportToJsonAsync(command.User.Id, command.User.Username);
        
        var fileName = $"config_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        
        using var stream = new MemoryStream(bytes);
        await command.FollowupWithFileAsync(stream, fileName, "Configuration export", ephemeral: true);
        
        LogInfo("Exported configuration for {Username}", command.User.Username);
    }
}