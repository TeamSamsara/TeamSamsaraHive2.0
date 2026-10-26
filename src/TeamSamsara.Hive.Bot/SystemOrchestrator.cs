using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using TeamSamsara.Hive.Bot.Configuration;
using TeamSamsara.Hive.Bot.Integrations.Discord;
using TeamSamsara.Hive.Bot.Integrations.Twitch;
using TeamSamsara.Hive.Bot.Tickets;
using TeamSamsara.Hive.Bot.Streams.Commands;
using TeamSamsara.Hive.Bot.Notifications;
using TeamSamsara.Hive.Bot.Protocols;

using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot;

// Root system orchestrator that coordinates all subsystems
// Manages initialization, startup, and shutdown of all orchestrators
public class SystemOrchestrator : BaseOrchestrator
{
    private readonly DiscordIntegration _discord;
    private readonly TwitchIntegration _twitch;
    
    public override string SystemName => "System";

    #region Constructor
    
    public SystemOrchestrator(
        DiscordIntegration discord,
        TwitchIntegration twitch,
        ILogger<SystemOrchestrator> logger,
        IServiceProvider serviceProvider)
        : base(logger, serviceProvider)
    {
        _discord = discord ?? throw new ArgumentNullException(nameof(discord));
        _twitch = twitch ?? throw new ArgumentNullException(nameof(twitch));
    }
    
    #endregion

    #region Lifecycle Management
    
    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await base.InitializeAsync(cancellationToken);
        
        LogInfo("===========================================");
        LogInfo("Team Samsara Hive v2 - System Initialization");
        LogInfo("===========================================");
        
        try
        {
            // Phase 1: Initialize Integrations
            LogInfo("Phase 1: Initializing Integrations...");
            
            await _discord.InitializeAsync(cancellationToken);
            LogInfo("✅ Discord Integration initialized");
            
            await _twitch.InitializeAsync(cancellationToken);
            LogInfo("✅ Twitch Integration initialized");
            
            // Phase 2: Initialize Configuration System
            LogInfo("Phase 2: Initializing Configuration System...");
            var configOrchestrator = ServiceProvider.GetRequiredService<ConfigurationOrchestrator>();
            await configOrchestrator.InitializeAsync(cancellationToken);
            LogInfo("✅ Configuration System initialized");
            
            // Phase 3: Initialize Ticket System
            LogInfo("Phase 3: Initializing Ticket System...");
            var ticketOrchestrator = ServiceProvider.GetRequiredService<TicketOrchestrator>();
            await ticketOrchestrator.InitializeAsync(cancellationToken);
            LogInfo("✅ Ticket System initialized");
            
            // Phase 4: Initialize Notification System
            LogInfo("Phase 4: Initializing Notification System...");
            var notificationOrchestrator = ServiceProvider.GetRequiredService<NotificationOrchestrator>();
            await notificationOrchestrator.InitializeAsync(cancellationToken);
            LogInfo("✅ Notification System initialized");
            
            // Phase 5: Register Stream Commands
            LogInfo("Phase 5: Registering Stream Commands...");
            var streamCommands = ServiceProvider.GetRequiredService<StreamCommandHandler>();
            streamCommands.RegisterEventHandlers();
            await streamCommands.RegisterCommandsAsync();
            LogInfo("✅ Stream Commands registered");
            
            // Phase 6: Protocols (only registered when using Firestore)
            var protocols = ServiceProvider.GetService<ProtocolOrchestrator>();
            if (protocols != null)
            {
                LogInfo("Phase 6: Initializing Protocols...");
                await protocols.InitializeAsync(cancellationToken);
                LogInfo("✅ Protocols initialized");
            }
            else
            {
                LogInfo("Protocols skipped (needs Database:Provider=Firestore)");
            }
            
            // Future: Full Analytics System
            // LogInfo("Phase X: Initializing Analytics System...");
            // await _analyticsOrchestrator.InitializeAsync(cancellationToken);
            // LogInfo("✅ Analytics System initialized");
            
            LogInfo("===========================================");
            LogInfo("✅ System Initialization Complete");
            LogInfo("===========================================");
        }
        catch (Exception ex)
        {
            LogError(ex, "System initialization failed");
            throw;
        }
    }
    
    public override async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await base.StartAsync(cancellationToken);
        
        LogInfo("===========================================");
        LogInfo("Team Samsara Hive v2 - System Starting");
        LogInfo("===========================================");
        
        try
        {
            // Start Configuration System
            var configOrchestrator = ServiceProvider.GetRequiredService<ConfigurationOrchestrator>();
            await configOrchestrator.StartAsync(cancellationToken);
            LogInfo("✅ Configuration System started");
            
            // Start Ticket System
            var ticketOrchestrator = ServiceProvider.GetRequiredService<TicketOrchestrator>();
            await ticketOrchestrator.StartAsync(cancellationToken);
            LogInfo("✅ Ticket System started");
            
            // Start Notification System
            var notificationOrchestrator = ServiceProvider.GetRequiredService<NotificationOrchestrator>();
            await notificationOrchestrator.StartAsync(cancellationToken);
            LogInfo("✅ Notification System started");
            
            var protocols = ServiceProvider.GetService<ProtocolOrchestrator>();
            if (protocols != null)
            {
                await protocols.StartAsync(cancellationToken);
                LogInfo("✅ Protocols started");
            }
            
            // Note: StreamMonitorService is registered as a HostedService in Program.cs,
            // so the .NET host starts/stops it automatically - no manual call needed here.
            
            // Future: Start Analytics System
            // await _analyticsOrchestrator.StartAsync(cancellationToken);
            // LogInfo("✅ Analytics System started");
            
            LogInfo("===========================================");
            LogInfo("✅ System Running - All Systems Operational");
            LogInfo("===========================================");
        }
        catch (Exception ex)
        {
            LogError(ex, "System startup failed");
            throw;
        }
    }
    
    public override async Task StopAsync(CancellationToken cancellationToken = default)
    {
        LogInfo("===========================================");
        LogInfo("Team Samsara Hive v2 - System Shutting Down");
        LogInfo("===========================================");
        
        try
        {
            // Stop in reverse order
            
            var protocols = ServiceProvider.GetService<ProtocolOrchestrator>();
            if (protocols != null)
            {
                await protocols.StopAsync(cancellationToken);
                LogInfo("✅ Protocols stopped");
            }
            
            var notificationOrchestrator = ServiceProvider.GetRequiredService<NotificationOrchestrator>();
            await notificationOrchestrator.StopAsync(cancellationToken);
            LogInfo("✅ Notification System stopped");
            
            var ticketOrchestrator = ServiceProvider.GetRequiredService<TicketOrchestrator>();
            await ticketOrchestrator.StopAsync(cancellationToken);
            LogInfo("✅ Ticket System stopped");
            
            var configOrchestrator = ServiceProvider.GetRequiredService<ConfigurationOrchestrator>();
            await configOrchestrator.StopAsync(cancellationToken);
            LogInfo("✅ Configuration System stopped");
            
            LogInfo("===========================================");
            LogInfo("✅ System Shutdown Complete");
            LogInfo("===========================================");
        }
        catch (Exception ex)
        {
            LogError(ex, "Error during system shutdown");
        }
        finally
        {
            await base.StopAsync(cancellationToken);
        }
    }
    
    public override async Task<bool> HealthCheckAsync()
    {
        if (!await base.HealthCheckAsync())
            return false;
        
        if (!await _discord.HealthCheckAsync())
        {
            LogWarning("Discord integration health check failed");
            return false;
        }
        
        if (!await _twitch.HealthCheckAsync())
        {
            LogWarning("Twitch integration health check failed");
            return false;
        }
        
        var configOrchestrator = ServiceProvider.GetRequiredService<ConfigurationOrchestrator>();
        if (!await configOrchestrator.HealthCheckAsync())
        {
            LogWarning("Configuration system health check failed");
            return false;
        }
        
        var ticketOrchestrator = ServiceProvider.GetRequiredService<TicketOrchestrator>();
        if (!await ticketOrchestrator.HealthCheckAsync())
        {
            LogWarning("Ticket system health check failed");
            return false;
        }
        
        var notificationOrchestrator = ServiceProvider.GetRequiredService<NotificationOrchestrator>();
        if (!await notificationOrchestrator.HealthCheckAsync())
        {
            LogWarning("Notification system health check failed");
            return false;
        }
        
        return true;
    }
    
    #endregion
}