using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Core.Interfaces;

namespace TeamSamsara.Hive.Core.Abstractions;

// Base class for all external API integrations
// Wraps external APIs like Twitch, Discord, etc.
// Provides HTTP client and logging functionality
public abstract class BaseIntegration : IIntegration
{
    // HTTP client for making API requests
    protected HttpClient HttpClient { get; }
    
    // Logger for this integration
    protected ILogger Logger { get; }
    
    // Name of this integration (override in derived classes)
    public abstract string IntegrationName { get; }
    
    // Whether this integration has been initialized
    public bool IsInitialized { get; protected set; }
    
    // Constructor requires HTTP client and logger
    protected BaseIntegration(HttpClient httpClient, ILogger logger)
    {
        HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        IsInitialized = false;
    }
    
    // Initialize the integration (override for authentication, token retrieval, etc.)
    public virtual async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        LogInfo("{IntegrationName} integration initializing...", IntegrationName);
        IsInitialized = true;
        await Task.CompletedTask;
    }
    
    // Health check (override to ping the external API)
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
    
    // Helper: Perform GET request and deserialize response
    protected async Task<T?> GetAsync<T>(string url)
    {
        try
        {
            var response = await HttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>();
        }
        catch (Exception ex)
        {
            LogError(ex, "Error calling GET {Url}", url);
            throw;
        }
    }
    
    // Helper: Perform POST request with JSON body and deserialize response
    protected async Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest request)
    {
        try
        {
            var response = await HttpClient.PostAsJsonAsync(url, request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TResponse>();
        }
        catch (Exception ex)
        {
            LogError(ex, "Error calling POST {Url}", url);
            throw;
        }
    }
}