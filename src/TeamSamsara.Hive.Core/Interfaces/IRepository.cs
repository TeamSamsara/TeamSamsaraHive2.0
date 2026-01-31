namespace TeamSamsara.Hive.Core.Interfaces;

// Interface for all repositories
// Repositories handle data access and storage operations
public interface IRepository
{
    // Name of this repository (for logging and debugging)
    string RepositoryName { get; }
    
    // Whether this repository is ready to be used
    bool IsInitialized { get; }
    
    // Initialize the repository (e.g., setup database connection)
    Task InitializeAsync(CancellationToken cancellationToken = default);
    
    // Check if the repository can access its data store
    Task<bool> HealthCheckAsync();
}