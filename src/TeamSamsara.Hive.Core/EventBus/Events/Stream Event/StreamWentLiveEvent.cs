using TeamSamsara.Hive.Core.EventBus.Events;

namespace TeamSamsara.Hive.Core.EventBus.Events;

// Event published when a Twitch stream goes live
// Published by : AnalyticsOrchestrator
// Subscribed by : NotificationOrchestrator

public class StreamWentLiveEvent : BaseEvent
{
    // Twitch username (lowercase)
    public string Username {get;set;}

    // Display name (with capitalization)
    public string DisplayName {get;set;}

    // Stream title/description
    public string StreamTitle {get;set;}

    // Game/category being played
    public string GameName {get;set;}

    // Current viewer count when stream went live
    public int ViewerCount {get;set;}

    // Twitch stream URL 
    public string StreamUrl { get; set; }

    // Stream thumbnail URL
    public string ThumbnailUrl { get; set; }

    // When the stream started (UTC)
    public DateTime StartedAt { get; set; }

    // Per-stream role override to ping instead of the global
    // StreamNotificationRoleId config value. Null = use the global one.
    public ulong? NotificationRoleId { get; set; }

    // Constructor - sets SourceSytem to "Analytics"
    public StreamWentLiveEvent() : base("Analytics")
    {
        Username = string.Empty;
        DisplayName = string.Empty;
        StreamTitle = string.Empty;
        GameName = string.Empty;
        StreamUrl = string.Empty;
        ThumbnailUrl = string.Empty;
    }

}