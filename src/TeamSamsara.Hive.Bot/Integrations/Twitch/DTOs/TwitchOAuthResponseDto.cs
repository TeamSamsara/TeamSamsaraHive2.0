using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Integrations.Twitch.DTOs;

// DTO for Twitch OAuth token endpoint response
// Maps the JSON response from Twitch OAuth /token endpoint
public class TwitchOAuthResponseDto : BaseDto
{
    // Access token for API authentication
    public string access_token { get; set; } = string.Empty;
    
    // Token expiration time in seconds
    public int expires_in { get; set; }
    
    // Token type (usually "bearer")
    public string token_type { get; set; } = string.Empty;
}