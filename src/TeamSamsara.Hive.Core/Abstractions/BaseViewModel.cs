namespace TeamSamsara.Hive.Core.Abstractions;

// Base class for all ViewModels
// Used for display/output objects like reports and summaries
// Includes metadata, caching, and versioning support
public abstract class BaseViewModel : BaseModel
{
    // When this view model was generated (useful for tracking freshness)
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    
    // What system generated this view model (e.g., "Analytics", "Tickets")
    public string? SourceSystem { get; set; }
    
    // Cache key for this view model (for expensive reports/calculations)
    public string? CacheKey { get; set; }
    
    // When this cached data expires (null = no expiration)
    public DateTime? ExpiresAtUtc { get; set; }
    
    // Version of this view model format (useful for API compatibility)
    public int Version { get; set; } = 1;
}