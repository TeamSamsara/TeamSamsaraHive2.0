using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Integrations.Twitch.DTOs;

// DTO wrapper for Twitch Helix API /users endpoint response
// Twitch API returns data in a wrapper with a "data" array
public class TwitchUserResponseDto : BaseDto
{
    // Array of user objects
    public List<TwitchUserDto> data { get; set; } = new();
}