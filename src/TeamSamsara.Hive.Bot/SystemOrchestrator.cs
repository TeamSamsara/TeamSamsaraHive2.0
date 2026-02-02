using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using TeamSamsara.Hive.Bot.Configuration;
using TeamSamsara.Hive.Bot.Integrations.Discord;
using TeamSamsara.Hive.Bot.Integrations.Twitch;

using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot;

// Root system orchestrator that coordinates all subsystems
// Manages initialization, startup, and shutdown of all orchestrators
public class SystemOrchestrator : BaseOrchestrator
{
    private readonly DiscordIntegration _discord;
    private readonly TwitchIntegration _twitch;
    
    // ConfigurationOrchestrator is now resolved via ServiceProvider when needed
    // This prevents singleton -> scoped dependency issues
    
    // Future orchestrators (Phase 3-5)
    // private readonly AnalyticsOrchestrator _analyticsOrchestrator;
    // private readonly NotificationOrchestrator _notificationOrchestrator;
    // private readonly TicketOrchestrator _ticketOrchestrator;
    
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
            
            // Phase 3-5: Future Systems
            // LogInfo("Phase 3: Initializing Analytics System...");
            // await _analyticsOrchestrator.InitializeAsync(cancellationToken);
            // LogInfo("✅ Analytics System initialized");
            
            // LogInfo("Phase 4: Initializing Notification System...");
            // await _notificationOrchestrator.InitializeAsync(cancellationToken);
            // LogInfo("✅ Notification System initialized");
            
            // LogInfo("Phase 5: Initializing Ticket System...");
            // await _ticketOrchestrator.InitializeAsync(cancellationToken);
            // LogInfo("✅ Ticket System initialized");
            
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
            
            // Future: Start other systems
            // await _analyticsOrchestrator.StartAsync(cancellationToken);
            // LogInfo("✅ Analytics System started");
            
            // await _notificationOrchestrator.StartAsync(cancellationToken);
            // LogInfo("✅ Notification System started");
            
            // await _ticketOrchestrator.StartAsync(cancellationToken);
            // LogInfo("✅ Ticket System started");
            
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
            
            // Future: Stop other systems
            // await _ticketOrchestrator.StopAsync(cancellationToken);
            // LogInfo("✅ Ticket System stopped");
            
            // await _notificationOrchestrator.StopAsync(cancellationToken);
            // LogInfo("✅ Notification System stopped");
            
            // await _analyticsOrchestrator.StopAsync(cancellationToken);
            // LogInfo("✅ Analytics System stopped");
            
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
        // Check base health
        if (!await base.HealthCheckAsync())
            return false;
        
        // Check integrations
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
        
        // Check Configuration System
        var configOrchestrator = ServiceProvider.GetRequiredService<ConfigurationOrchestrator>();
        if (!await configOrchestrator.HealthCheckAsync())
        {
            LogWarning("Configuration system health check failed");
            return false;
        }
        
        // Future: Check other systems
        // if (!await _analyticsOrchestrator.HealthCheckAsync()) return false;
        // if (!await _notificationOrchestrator.HealthCheckAsync()) return false;
        // if (!await _ticketOrchestrator.HealthCheckAsync()) return false;
        
        return true;
    }
    
    #endregion
}