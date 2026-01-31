namespace TeamSamsara.Hive.Core.Interfaces;

// Interface for all services
// Services implement specific business operations (one service = one operation)
public interface IService
{
    // Name of this service (for logging and debugging)
    string ServiceName { get; }
    
    // Whether this service is ready to be used
    bool IsInitialized { get; }
    
    // Initialize the service (if needed)
    Task InitializeAsync(CancellationToken cancellationToken = default);
    
    // Check if the service is healthy and operational
    Task<bool> HealthCheckAsync();
}