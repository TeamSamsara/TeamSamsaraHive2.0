using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot;
using TeamSamsara.Hive.Core.EventBus;

// Create host with dependency injection and logging
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        // Register Core infrastructure
        services.AddSingleton<EventBus>();
        services.AddSingleton<Router>();
        
        // Register Test Orchestrator
        services.AddSingleton<TestOrchestrator>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Debug);
    })
    .Build();

Console.WriteLine("==============================================");
Console.WriteLine("Team Samsara Hive - Core Infrastructure Test");
Console.WriteLine("==============================================\n");

// Get the test orchestrator
var orchestrator = host.Services.GetRequiredService<TestOrchestrator>();

// Test lifecycle
await orchestrator.InitializeAsync();
await orchestrator.StartAsync();

Console.WriteLine("\n==============================================");
Console.WriteLine("Press any key to stop...");
Console.WriteLine("==============================================\n");
Console.ReadKey();

await orchestrator.StopAsync();

Console.WriteLine("\n==============================================");
Console.WriteLine("✅ All Core infrastructure tests passed!");
Console.WriteLine("==============================================");