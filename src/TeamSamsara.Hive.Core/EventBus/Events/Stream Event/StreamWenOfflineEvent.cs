using TeamSamsara.Hive.Core.EventBus.Events;

namespace TeamSamsara.Hive.Core.EventBus.Events;

// Event published when a Twitch stream goes offline
// Published by: AnalyticsOrchestrator
// Subscribed by: NotificationOrchestrator (optional - for offline notifications)
public class StreamWentOfflineEvent : BaseEvent
{
    // Twitch username (lowercase)
    public string Username { get; set; }
    
    // Display name (with capitalization)
    public string DisplayName { get; set; }
    
    // When the stream ended (UTC)
    public DateTime EndedAt { get; set; }
    
    // Total stream duration in minutes
    public int DurationMinutes { get; set; }
    
    // Average viewer count during stream (nullable if not calculated)
    public int? AverageViewers { get; set; }
    
    // Peak viewer count during stream (nullable if not calculated)
    public int? PeakViewers { get; set; }
    
    // Constructor - sets SourceSystem to "Analytics"
    public StreamWentOfflineEvent() : base("Analytics")
    {
        // Required properties will be set by AnalyticsOrchestrator
        Username = string.Empty;
        DisplayName = string.Empty;
    }
}