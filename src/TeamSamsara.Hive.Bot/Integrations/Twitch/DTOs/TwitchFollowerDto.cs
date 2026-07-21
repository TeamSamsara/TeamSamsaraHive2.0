using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Integrations.Twitch.DTOs;

// DTO for Twitch Helix API /channels/followers endpoint response
// Maps the JSON response from Twitch API
public class TwitchFollowerDto : BaseDto
{
    // User ID of the follower
    public string user_id { get; set; } = string.Empty;
    
    // User login name of the follower (lowercase username)
    public string user_login { get; set; } = string.Empty;
    
    // User display name of the follower (formatted username)
    public string user_name { get; set; } = string.Empty;
    
    // When the user started following the broadcaster
    public DateTime followed_at { get; set; }
}

// Wrapper DTO for the full follower response (includes pagination)
public class TwitchFollowerResponseDto : BaseDto
{
    // Total number of followers
    public int total { get; set; }
    
    // List of followers
    public List<TwitchFollowerDto> data { get; set; } = new();
    
    // Pagination cursor (for fetching next page)
    public PaginationInfo? pagination { get; set; }
}

// Nested class for pagination information
public class PaginationInfo
{
    // Cursor for the next page of results
    public string cursor { get; set; } = string.Empty;
}