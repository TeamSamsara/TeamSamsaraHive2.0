using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using TeamSamsara.Hive.Bot;
using TeamSamsara.Hive.Bot.Configuration;
using TeamSamsara.Hive.Bot.Configuration.Data;
using TeamSamsara.Hive.Bot.Configuration.Repositories;
using TeamSamsara.Hive.Bot.Configuration.Services;
using TeamSamsara.Hive.Bot.Integrations.Discord;
using TeamSamsara.Hive.Bot.Integrations.Twitch;
using TeamSamsara.Hive.Bot.Tickets;
using TeamSamsara.Hive.Bot.Tickets.Data;
using TeamSamsara.Hive.Bot.Tickets.Repositories;
using TeamSamsara.Hive.Bot.Tickets.Services;
using TeamSamsara.Hive.Bot.Streams.Commands;
using TeamSamsara.Hive.Bot.Streams.Data;
using TeamSamsara.Hive.Bot.Streams.Repositories;
using TeamSamsara.Hive.Bot.Streams.Services;
using TeamSamsara.Hive.Bot.Notifications;
using TeamSamsara.Hive.Core.EventBus;
using TeamSamsara.Hive.Core.Workflows.Abstractions;

// Determine environment
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

Console.WriteLine("==============================================");
Console.WriteLine($"Team Samsara Hive v2 - Starting ({environment})");
Console.WriteLine("==============================================\n");

// Create host with dependency injection and logging
var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        // Load base configuration
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        
        // Load environment-specific configuration
        config.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);
        
        // Add environment variables (highest priority)
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;
        
        // ═══════════════════════════════════════════════════════
        // CORE INFRASTRUCTURE
        // ═══════════════════════════════════════════════════════
        services.AddSingleton<EventBus>();
        services.AddSingleton<Router>();
        services.AddMemoryCache(); // Required for ConfigurationRepository caching
        
        // ═══════════════════════════════════════════════════════
        // INTEGRATIONS (Phase 1)
        // ═══════════════════════════════════════════════════════
        services.AddHttpClient(); // Required for integrations
        services.AddSingleton<DiscordIntegration>();
        services.AddSingleton<TwitchIntegration>();
        
        // ═══════════════════════════════════════════════════════
        // CONFIGURATION SYSTEM (Phase 2)
        // ═══════════════════════════════════════════════════════
        
        // Database
        services.AddDbContext<ConfigurationDbContext>(options =>
            options.UseSqlite("Data Source=data/configuration.db"));
        
        // Data Layer
        services.AddScoped<ConfigurationQueryOptimizer>();
        
        // Repositories
        services.AddScoped<IConfigurationRepository, ConfigurationRepository>();
        
        // Services
        services.AddScoped<GetConfigValueService>();
        services.AddScoped<SetConfigValueService>();
        services.AddScoped<ValidateConfigService>();
        services.AddScoped<ExportConfigService>();
        services.AddScoped<ImportConfigService>();
        
        // Orchestrator
        services.AddSingleton<ConfigurationOrchestrator>();
        
        // ═══════════════════════════════════════════════════════
        // TICKET SYSTEM (Phase 5)
        // ═══════════════════════════════════════════════════════
        
        // Database
        services.AddDbContext<TicketDbContext>(options =>
            options.UseSqlite("Data Source=data/tickets.db"));
        
        // Data Layer
        services.AddScoped<TicketQueryOptimizer>();
        services.AddScoped<IStatusTransition, TicketTransitionService>();
        
        // Repositories
        services.AddScoped<ITicketRepository, TicketRepository>();
        
        // Services
        services.AddScoped<TicketService>();
        services.AddScoped<TicketFormService>();
        services.AddScoped<TicketNotificationService>();
        services.AddScoped<TicketExportService>();
        
        // Orchestrator
        services.AddSingleton<TicketOrchestrator>();
        
        // ═══════════════════════════════════════════════════════
        // STREAM TRACKING
        // ═══════════════════════════════════════════════════════
        
        // Database
        services.AddDbContext<StreamDbContext>(options =>
            options.UseSqlite("Data Source=data/streams.db"));
        
        // Repository
        services.AddScoped<IStreamRepository, StreamRepository>();
        
        // Services
        services.AddScoped<AddStreamService>();
        
        // Commands
        services.AddSingleton<StreamCommandHandler>();
        
        // Background watcher - polls Twitch, publishes events
        services.AddHostedService<StreamMonitorService>();
        
        // ═══════════════════════════════════════════════════════
        // NOTIFICATION SYSTEM
        // ═══════════════════════════════════════════════════════
        
        services.AddScoped<StreamNotificationService>();
        services.AddSingleton<NotificationOrchestrator>();
        
        // ═══════════════════════════════════════════════════════
        // FUTURE SYSTEMS (Phase 3)
        // ═══════════════════════════════════════════════════════
        // Full Analytics System (Phase 3) - snapshots, viewer sampling, reports
        // services.AddSingleton<AnalyticsOrchestrator>();
        
        // ═══════════════════════════════════════════════════════
        // SYSTEM ORCHESTRATOR (Root)
        // ═══════════════════════════════════════════════════════
        services.AddSingleton<SystemOrchestrator>();
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        
        // Set log level based on environment
        var logLevel = environment switch
        {
            "Development" => LogLevel.Debug,
            "Staging" => LogLevel.Information,
            "Production" => LogLevel.Information,
            _ => LogLevel.Information
        };
        
        logging.SetMinimumLevel(logLevel);
    })
    .Build();

try
{
    // ═══════════════════════════════════════════════════════
    // DATABASE INITIALIZATION
    // ═══════════════════════════════════════════════════════
    using (var scope = host.Services.CreateScope())
    {
        Console.WriteLine("Initializing databases...");
        
        // Initialize Configuration Database
        var configDb = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
        await configDb.Database.EnsureCreatedAsync();
        Console.WriteLine("✅ Configuration database ready");
        
        // Initialize Ticket Database
        var ticketDb = scope.ServiceProvider.GetRequiredService<TicketDbContext>();
        await ticketDb.Database.EnsureCreatedAsync();
        Console.WriteLine("✅ Ticket database ready");
        
        // Initialize Stream database
        var streamDb = scope.ServiceProvider.GetRequiredService<StreamDbContext>();
        await streamDb.Database.EnsureCreatedAsync();
        Console.WriteLine("✅ Stream database ready");
        
        Console.WriteLine();
    }
    
    // Get the system orchestrator
    var orchestrator = host.Services.GetRequiredService<SystemOrchestrator>();
    
    // Initialize and start the system
    await orchestrator.InitializeAsync();
    await orchestrator.StartAsync();
    
    Console.WriteLine("\n==============================================");
    Console.WriteLine("Press Ctrl+C to stop...");
    Console.WriteLine("==============================================\n");
    
    // Wait for shutdown signal
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (sender, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };
    
    await Task.Delay(-1, cts.Token);
}
catch (OperationCanceledException)
{
    // Graceful shutdown
    Console.WriteLine("\n==============================================");
    Console.WriteLine("Shutting down...");
    Console.WriteLine("==============================================\n");
}
catch (Exception ex)
{
    Console.WriteLine($"\n❌ Fatal error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    return 1;
}
finally
{
    var orchestrator = host.Services.GetService<SystemOrchestrator>();
    if (orchestrator != null)
    {
        await orchestrator.StopAsync();
    }
}

return 0;