using Google.Cloud.Firestore;
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
using TeamSamsara.Hive.Bot.Database;
using TeamSamsara.Hive.Bot.Database.Firestore;
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

// Load local settings from a .env file, if there is one (searched for in the
// current folder and its parents, so `dotnet run` from the project folder
// finds the .env at the repo root). .env is git-ignored and is never deployed,
// so on Render this does nothing. NoClobber: real environment variables
// always win over .env values.
DotNetEnv.Env.NoClobber().TraversePath().Load();

// Determine environment
var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

Console.WriteLine("==============================================");
Console.WriteLine($"Team Samsara Hive v2 - Starting ({environment})");
Console.WriteLine("==============================================\n");

// Fixed, absolute path for all SQLite databases. Using an absolute path here
// (instead of the old relative "data/xxx.db") means persistence no longer
// depends on guessing the container's working directory - whatever platform
// this runs on (Railway, Docker, etc.), just mount a persistent volume at
// this exact path and the data survives redeploys/restarts.
const string DataDirectory = "/data";

// Ensure the directory exists. If a volume is mounted at DataDirectory,
// this is a no-op (already exists). If no volume is mounted (e.g. local
// dev), this creates a plain local folder so the app still runs - it just
// won't persist across container rebuilds without a volume attached.
Directory.CreateDirectory(DataDirectory);

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
        // DATABASE PROVIDER
        // ═══════════════════════════════════════════════════════
        // Database:Provider picks the storage backend: "Sqlite" (local files,
        // wiped on every Render restart on the free plan) or "Firestore"
        // (survives restarts). Only the repository registrations below differ.
        var databaseSettings = DatabaseSettings.FromConfiguration(configuration);
        var useFirestore = databaseSettings.Provider == DatabaseProvider.Firestore;
        services.AddSingleton(databaseSettings);
        
        if (useFirestore)
        {
            // One shared, thread-safe client for the whole bot
            services.AddSingleton<FirestoreDb>(sp => FirestoreFactory.Create(
                databaseSettings,
                sp.GetRequiredService<ILoggerFactory>().CreateLogger("Firestore")));
        }
        
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
            options.UseSqlite($"Data Source={DataDirectory}/configuration.db"));
        
        // Data Layer
        services.AddScoped<ConfigurationQueryOptimizer>();
        
        // Repositories
        // Firestore version is a singleton because it keeps an in-memory cache
        if (useFirestore)
            services.AddSingleton<IConfigurationRepository, FirestoreConfigurationRepository>();
        else
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
            options.UseSqlite($"Data Source={DataDirectory}/tickets.db"));
        
        // Data Layer
        services.AddScoped<TicketQueryOptimizer>();
        services.AddScoped<IStatusTransition, TicketTransitionService>();
        
        // Repositories
        if (useFirestore)
        {
            services.AddScoped<ITicketRepository, FirestoreTicketRepository>();
            services.AddScoped<IStatusTransitionStore, FirestoreStatusTransitionStore>();
        }
        else
        {
            services.AddScoped<ITicketRepository, TicketRepository>();
            services.AddScoped<IStatusTransitionStore, SqliteStatusTransitionStore>();
        }
        
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
            options.UseSqlite($"Data Source={DataDirectory}/streams.db"));
        
        // Repository
        // Firestore version is a singleton because it keeps an in-memory cache
        if (useFirestore)
            services.AddSingleton<IStreamRepository, FirestoreStreamRepository>();
        else
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
    var databaseSettings = host.Services.GetRequiredService<DatabaseSettings>();
    
    if (databaseSettings.Provider == DatabaseProvider.Firestore)
    {
        // Fail fast if the key, project ID or database is wrong
        Console.WriteLine("Connecting to Firestore...");
        await FirestoreFactory.VerifyConnectionAsync(host.Services.GetRequiredService<FirestoreDb>());
        Console.WriteLine($"✅ Firestore ready (project {databaseSettings.FirestoreProjectId})\n");
    }
    else
    using (var scope = host.Services.CreateScope())
    {
        Console.WriteLine("Initializing SQLite databases...");
        
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

        // EnsureCreatedAsync only creates tables that don't exist yet - it does
        // NOT alter existing tables. streams.db already had a TrackedStreams
        // table from before NotificationRoleId was added to the entity, so we
        // add the column by hand here if it's missing. Safe to run every
        // startup: it's a no-op once the column exists.
        await EnsureNotificationRoleColumnAsync(streamDb);
        
        Console.WriteLine();
    }
    
    // Get the system orchestrator
    var orchestrator = host.Services.GetRequiredService<SystemOrchestrator>();
    
    // Initialize the system FIRST - this is what authenticates TwitchIntegration
    // (Phase 1) and connects Discord, among other things. It must fully complete
    // before we start any hosted background services below.
    await orchestrator.InitializeAsync();
    
    // NOW start the .NET Generic Host itself. This triggers every service
    // registered via AddHostedService<T> (e.g. StreamMonitorService) to have
    // its StartAsync/ExecuteAsync invoked. StreamMonitorService polls Twitch
    // immediately on its first cycle with no startup delay, so this MUST come
    // after orchestrator.InitializeAsync() - otherwise the poller's first
    // Twitch API call races ahead of Twitch OAuth and fails with 401
    // Unauthorized (TwitchIntegration's HttpClient has no auth header yet).
    await host.StartAsync();
    
    await orchestrator.StartAsync();
    
    Console.WriteLine("\n==============================================");
    Console.WriteLine("Press Ctrl+C to stop...");
    Console.WriteLine("==============================================\n");
    
    // Block until a shutdown signal (Ctrl+C / SIGTERM) is received, then
    // gracefully stops the host (and all hosted services, including
    // StreamMonitorService) before returning.
    await host.WaitForShutdownAsync();
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

// 0 on a normal shutdown; 1 if something (e.g. the Discord connection
// watchdog) stopped the app because it needs a restart.
return Environment.ExitCode;

// Checks whether TrackedStreams.NotificationRoleId exists via PRAGMA table_info,
// and adds it with ALTER TABLE if it's missing. Lets the per-stream role-ping
// feature work against a streams.db created before that column existed,
// without wiping existing tracked streams.
static async Task EnsureNotificationRoleColumnAsync(StreamDbContext streamDb)
{
    var connection = streamDb.Database.GetDbConnection();

    if (connection.State != System.Data.ConnectionState.Open)
    {
        await connection.OpenAsync();
    }

    var hasColumn = false;

    await using (var checkCmd = connection.CreateCommand())
    {
        checkCmd.CommandText = "PRAGMA table_info(TrackedStreams);";
        await using var reader = await checkCmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            // Column index 1 in PRAGMA table_info's result set is the column name
            if (string.Equals(reader.GetString(1), "NotificationRoleId", StringComparison.OrdinalIgnoreCase))
            {
                hasColumn = true;
                break;
            }
        }
    }

    if (!hasColumn)
    {
        await using var alterCmd = connection.CreateCommand();
        alterCmd.CommandText = "ALTER TABLE TrackedStreams ADD COLUMN NotificationRoleId INTEGER NULL;";
        await alterCmd.ExecuteNonQueryAsync();
        Console.WriteLine("✅ Migrated streams.db: added NotificationRoleId column");
    }
}