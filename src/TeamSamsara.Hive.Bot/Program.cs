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

using TeamSamsara.Hive.Core.EventBus;

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
        
        // Memory cache for repository caching
        services.AddMemoryCache();
        
        // ═══════════════════════════════════════════════════════
        // DATABASE (Configuration System)
        // ═══════════════════════════════════════════════════════
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Data Source=data/configuration.db";
        
        services.AddDbContext<ConfigurationDbContext>(options =>
        {
            options.UseSqlite(connectionString);
            
            // Enable detailed errors in Development
            if (environment == "Development")
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });
        
        // ═══════════════════════════════════════════════════════
        // INTEGRATIONS (Phase 1)
        // ═══════════════════════════════════════════════════════
        services.AddHttpClient(); // Required for integrations
        services.AddSingleton<DiscordIntegration>();
        services.AddSingleton<TwitchIntegration>();
        
        // ═══════════════════════════════════════════════════════
        // CONFIGURATION SYSTEM (Phase 2)
        // ═══════════════════════════════════════════════════════
        
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
        // FUTURE SYSTEMS (Phase 3-5)
        // ═══════════════════════════════════════════════════════
        // Analytics System (Phase 3)
        // services.AddDbContext<AnalyticsDbContext>(options => 
        //     options.UseSqlite("Data Source=data/analytics.db"));
        // services.AddScoped<AnalyticsQueryOptimizer>();
        // services.AddSingleton<AnalyticsOrchestrator>();
        
        // Notification System (Phase 4)
        // services.AddSingleton<NotificationOrchestrator>();
        
        // Ticket System (Phase 5)
        // services.AddDbContext<TicketDbContext>(options => 
        //     options.UseSqlite("Data Source=data/tickets.db"));
        // services.AddScoped<TicketQueryOptimizer>();
        // services.AddSingleton<TicketOrchestrator>();
        
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
        var configDbContext = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
        
        // Ensure database is created
        await configDbContext.Database.EnsureCreatedAsync();
        
        Console.WriteLine("✅ Configuration database initialized\n");
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