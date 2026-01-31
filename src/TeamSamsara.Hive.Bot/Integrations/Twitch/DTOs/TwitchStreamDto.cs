using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Integrations.Twitch.DTOs;

// DTO for Twitch Helix API /streams endpoint response
// Maps the JSON response from Twitch API
public class TwitchStreamDto : BaseDto
{
    // Unique Identifier
    public string id { get; set; } = string.Empty;

    // User ID of the broadcaster
    public string user_id { get; set; } = string.Empty;

    // user login name
    public string user_login { get; set; } = string.Empty;

    // user display name
    public string user_name { get; set; } = string.Empty;

    // Game ID being played
    public string game_id { get; set; } = string.Empty;

    // Game name being played
    public string game_name { get; set; } = string.Empty;

    // Stream Type
    public string type { get; set; } = string.Empty;

    // Stream title
    public string title { get; set; } = string.Empty;

    // Current viewer count 
    public int viewer_count { get; set; } 

    // When the stream started
    public DateTime started_at { get; set; }

    // Stream language (e.g ''EN'')
    public string language { get; set; } = string.Empty;

    // Thumbnail URL template
    public string thumbnail_url { get; set; } = string.Empty;

    // Tag IDs associated with the stream
    public List<string> tag_ids { get; set; } = new();

    // Tags associated with the stream
    public List<string> tags { get; set; } = new();

    // Whether the stream is mature content or not
    public bool is_mature { get; set; }
}