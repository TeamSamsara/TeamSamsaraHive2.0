using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Streams.Models.Entities;

// Represents a Twitch stream being tracked for live/offline notifications
// One row per streamer added via /stream add
public class TrackedStreamEntity : BaseEntity
{
    public string TwitchUsername { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public ulong AddedByUserId { get; set; }
    public string AddedByUsername { get; set; } = string.Empty;
    public DateTime AddedAtUtc { get; set; }

    public bool IsLive { get; set; }

    public DateTime? LastLiveAtUtc { get; set; }
    public DateTime? LastOfflineAtUtc { get; set; }
    public string? LastStreamTitle { get; set; }
    public string? LastGameName { get; set; }

    // Optional override for which role gets pinged when THIS stream goes live.
    // Null means "use the global StreamNotificationRoleId config value instead".
    // Set via /stream add (role option) or /stream set-role.
    public ulong? NotificationRoleId { get; set; }
}