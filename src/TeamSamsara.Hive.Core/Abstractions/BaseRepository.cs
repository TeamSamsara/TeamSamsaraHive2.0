using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Core.Interfaces;

namespace TeamSamsara.Hive.Core.Abstractions;

// Base class for all repositories
// Repositories handle data access and storage operations
// Provides logging functionality
public abstract class BaseRepository : IRepository
{
    // Logger for this repository
    protected ILogger Logger { get; }
    
    // Name of this repository (override in derived classes)
    public abstract string RepositoryName { get; }
    
    // Whether this repository has been initialized
    public bool IsInitialized { get; protected set; }
    
    // Constructor requires logger
    protected BaseRepository(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        IsInitialized = false;
    }
    
    // Initialize the repository (override if repository needs initialization)
    public virtual async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        LogDebug("{RepositoryName} initializing...", RepositoryName);
        IsInitialized = true;
        await Task.CompletedTask;
    }
    
    // Health check (override if repository has specific health checks)
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
    
    // Helper: Log error with exception
    protected void LogError(Exception exception, string message, params object[] args)
    {
        Logger.LogError(exception, message, args);
    }
    
    // Helper: Log warning message
    protected void LogWarning(string message, params object[] args)
    {
        Logger.LogWarning(message, args);
    }
    
    // Helper: Log debug message
    protected void LogDebug(string message, params object[] args)
    {
        Logger.LogDebug(message, args);
    }
}