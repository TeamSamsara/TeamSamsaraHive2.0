using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Integrations.Twitch.DTOs;

// DTO for Twitch Helix API /videos endpoint response
// Maps the JSON response from Twitch API
public class TwitchVODDto : BaseDto
{
    // Unique video ID
    public string id { get; set; } = string.Empty;
    
    // Stream ID (if this VOD is from a stream)
    public string stream_id { get; set; } = string.Empty;
    
    // User ID of the broadcaster
    public string user_id { get; set; } = string.Empty;
    
    // User login name (lowercase username)
    public string user_login { get; set; } = string.Empty;
    
    // User display name (formatted username)
    public string user_name { get; set; } = string.Empty;
    
    // Video title
    public string title { get; set; } = string.Empty;
    
    // Video description
    public string description { get; set; } = string.Empty;
    
    // When the video was created
    public DateTime created_at { get; set; }
    
    // When the video was published
    public DateTime published_at { get; set; }
    
    // Video URL
    public string url { get; set; } = string.Empty;
    
    // Thumbnail URL template
    public string thumbnail_url { get; set; } = string.Empty;
    
    // Whether the video is viewable
    public string viewable { get; set; } = string.Empty;
    
    // View count
    public int view_count { get; set; }
    
    // Video language (e.g., "en")
    public string language { get; set; } = string.Empty;
    
    // Video type ("upload", "archive", "highlight")
    public string type { get; set; } = string.Empty;
    
    // Video duration (e.g., "3h8m33s")
    public string duration { get; set; } = string.Empty;
    
    // Muted segments (for copyright)
    public List<MutedSegment>? muted_segments { get; set; }
}

// Nested class for muted segments within VODs
public class MutedSegment
{
    // Duration of the muted segment in seconds
    public int duration { get; set; }
    
    // Offset in seconds from the start of the video
    public int offset { get; set; }
}