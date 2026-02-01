using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Integrations.Twitch.DTOs;

// DTO wrapper for Twitch Helix API /videos endpoint response
// Twitch API returns data in a wrapper with a "data" array
public class TwitchVODResponseDto : BaseDto
{
    // Array of video/VOD objects
    public List<TwitchVODDto> data { get; set; } = new();
}