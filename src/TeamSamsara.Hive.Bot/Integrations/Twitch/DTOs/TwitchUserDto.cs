using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Integrations.Twitch.DTOs;

// DTO for Twitch Helix API /users endpoint response
// Maps the JSON response from Twitch API
public class TwitchUserDto : BaseDto
{
    // Unique user ID
    public string id { get; set; } = string.Empty;
    
    // User login name (lowercase username)
    public string login { get; set; } = string.Empty;
    
    // User display name (formatted username)
    public string display_name { get; set; } = string.Empty;
    
    // User type (e.g., "staff", "admin", "global_mod", or empty string)
    public string type { get; set; } = string.Empty;
    
    // Broadcaster type (e.g., "partner", "affiliate", or empty string)
    public string broadcaster_type { get; set; } = string.Empty;
    
    // User description/bio
    public string description { get; set; } = string.Empty;
    
    // Profile image URL
    public string profile_image_url { get; set; } = string.Empty;
    
    // Offline image URL
    public string offline_image_url { get; set; } = string.Empty;
    
    // Total view count on the channel
    public int view_count { get; set; }
    
    // Email address (only returned if user:read:email scope is granted)
    public string email { get; set; } = string.Empty;
    
    // When the user account was created
    public DateTime created_at { get; set; }
}