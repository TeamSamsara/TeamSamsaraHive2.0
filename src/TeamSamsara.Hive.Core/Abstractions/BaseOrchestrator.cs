using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Core.Interfaces;

namespace TeamSamsara.Hive.Core.Abstractions;

// Base class for all system orchestrators
// Orchestrators coordinate services and manage system lifecycle
// Each system (Analytics, Notifications, Tickets) has ONE orchestrator
public abstract class BaseOrchestrator : IOrchestrator
{
    // Logger for this orchestrator
    protected ILogger Logger { get; }
    
    // Service provider for resolving dependencies
    protected IServiceProvider ServiceProvider { get; }
    
    // Name of this system (e.g., "Analytics", "Notifications", "Tickets")
    public abstract string SystemName { get; }
    
    // Whether this orchestrator has been initialized
    public bool IsInitialized { get; protected set; }
    
    // Whether this orchestrator is currently running
    public bool IsRunning { get; protected set; }
    
    // Constructor requires logger and service provider
    protected BaseOrchestrator(ILogger logger, IServiceProvider serviceProvider)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        IsInitialized = false;
        IsRunning = false;
    }
    
    // Initialize the system - called once before Start
    public virtual async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("{SystemName} system initializing...", SystemName);
        IsInitialized = true;
        await Task.CompletedTask;
    }
    
    // Start the system - called after Initialize
    public virtual async Task StartAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("{SystemName} system starting...", SystemName);
        IsRunning = true;
        await Task.CompletedTask;
    }
    
    // Stop the system - called during shutdown
    public virtual async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("{SystemName} system stopping...", SystemName);
        IsRunning = false;
        await Task.CompletedTask;
    }
    
    // Check if the system is healthy and operational (override for specific checks)
    public virtual async Task<bool> HealthCheckAsync()
    {
        await Task.CompletedTask;
        return IsInitialized && IsRunning;
    }
    
    // Helper: Resolve a service from the service provider
    protected T GetService<T>() where T : notnull
    {
        return ServiceProvider.GetRequiredService<T>();
    }
    
    // Helper: Log informational message
    protected void LogInfo(string message, params object[] args)
    {
        Logger.LogInformation(message, args);
    }
    
    // Helper: Log warning message
    protected void LogWarning(string message, params object[] args)
    {
        Logger.LogWarning(message, args);
    }
    
    // Helper: Log error with exception
    protected void LogError(Exception exception, string message, params object[] args)
    {
        Logger.LogError(exception, message, args);
    }
}