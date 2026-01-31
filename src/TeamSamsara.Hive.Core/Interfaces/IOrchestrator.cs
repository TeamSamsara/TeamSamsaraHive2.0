namespace TeamSamsara.Hive.Core.Interfaces;

// Contract for all system orchestrators
// Defines the lifecycle methods all orchestrators must implement
public interface IOrchestrator
{
    // Name of this system
    string SystemName { get; }
    
    // Whether this orchestrator has been initialized
    bool IsInitialized { get; }
    
    // Whether this orchestrator is currently running
    bool IsRunning { get; }
    
    // Initialize the system - called once before Start
    Task InitializeAsync(CancellationToken cancellationToken = default);
    
    // Start the system - called after Initialize
    Task StartAsync(CancellationToken cancellationToken = default);
    
    // Stop the system - called during shutdown
    Task StopAsync(CancellationToken cancellationToken = default);
    
    // Check if the system is healthy and operational
    Task<bool> HealthCheckAsync();
}