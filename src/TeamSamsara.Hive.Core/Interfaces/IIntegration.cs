namespace TeamSamsara.Hive.Core.Interfaces;

// Interface for all external API integrations
// Integrations wrap external APIs like Twitch, Discord, etc.
public interface IIntegration
{
    // Name of this integration (e.g., "Twitch", "Discord")
    string IntegrationName { get; }
    
    // Whether this integration is ready to be used
    bool IsInitialized { get; }
    
    // Initialize the integration (e.g., authenticate, get tokens)
    Task InitializeAsync(CancellationToken cancellationToken = default);
    
    // Check if the integration can reach the external API
    Task<bool> HealthCheckAsync();
}