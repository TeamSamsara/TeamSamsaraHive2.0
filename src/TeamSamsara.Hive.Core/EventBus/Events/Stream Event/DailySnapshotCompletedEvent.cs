using TeamSamsara.Hive.Core.EventBus.Events;

namespace TeamSamsara.Hive.Core.EventBus.Events;

// Event published when the daily analytics snapshot completes
// Published by: AnalyticsOrchestrator
// Subscribed by: NotificationOrchestrator (for admin reports)
public class DailySnapshotCompletedEvent : BaseEvent
{
    // The date of the snapshot (e.g., 2026-02-01 for Feb 1st data)
    public DateTime SnapshotDate { get; set; }
    
    // Number of Twitch snapshots created (one per monitored streamer)
    public int TwitchSnapshotsCreated { get; set; }
    
    // Number of Discord snapshots created (typically 1 per day, or 0 if failed)
    public int DiscordSnapshotsCreated { get; set; }
    
    // Whether all snapshots completed successfully
    public bool Success { get; set; }
    
    // Error message if snapshot failed (nullable)
    public string? ErrorMessage { get; set; }
    
    // Constructor - sets SourceSystem to "Analytics"
    public DailySnapshotCompletedEvent() : base("Analytics")
    {
        // Required properties will be set by AnalyticsOrchestrator
    }
}