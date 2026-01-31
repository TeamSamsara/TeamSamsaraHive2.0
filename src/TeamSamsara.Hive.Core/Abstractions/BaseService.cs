using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Core.Interfaces;

namespace TeamSamsara.Hive.Core.Abstractions;

// Base class for all services
// Services perform specific business operations (one service = one operation)
// Provides logging and configuration access
public abstract class BaseService : IService
{
    // Logger for this service
    protected ILogger Logger { get; }
    
    // Application configuration access
    protected IConfiguration Configuration { get; }
    
    // Name of this service (override in derived classes)
    public abstract string ServiceName { get; }
    
    // Whether this service has been initialized
    public bool IsInitialized { get; protected set; }
    
    // Constructor requires logger and configuration
    protected BaseService(ILogger logger, IConfiguration configuration)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        IsInitialized = false;
    }
    
    // Initialize the service (override if service needs initialization)
    public virtual async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        LogDebug("{ServiceName} initializing...", ServiceName);
        IsInitialized = true;
        await Task.CompletedTask;
    }
    
    // Health check (override if service has specific health checks)
    public virtual async Task<bool> HealthCheckAsync()
    {
        await Task.CompletedTask;
        return IsInitialized;
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
    
    // Helper: Log debug message
    protected void LogDebug(string message, params object[] args)
    {
        Logger.LogDebug(message, args);
    }
}