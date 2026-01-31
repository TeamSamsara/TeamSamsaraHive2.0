using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Integrations.Twitch.DTOs;

// DTO wrapper for Twitch Helix API /streams endpoint response
// Twitch API returns data in a wrapper with a "data" array
public class TwitchStreamResponseDto : BaseDto
{
    // Array of stream objects (empty if offline)
    public List<TwitchStreamDto> data { get; set; } = new();
}