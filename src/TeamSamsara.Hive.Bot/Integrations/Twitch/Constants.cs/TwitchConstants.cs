namespace TeamSamsara.Hive.Bot.Integrations.Twitch.Constants;

// All string constants for Twitch API integration
public static class TwitchConstants
{
    // API Base URLs
    public const string ApiBaseUrl = "https://api.twitch.tv/helix";
    public const string OAuthBaseUrl = "https://id.twitch.tv/oauth2";
    
    // API Endpoints
    public const string StreamsEndpoint = "/streams";
    public const string UsersEndpoint = "/users";
    public const string VideosEndpoint = "/videos";
    public const string FollowsEndpoint = "/channels/followers";
    
    // HTTP Headers
    public const string ClientIdHeader = "Client-Id";
    public const string AuthorizationHeader = "Authorization";
    public const string AuthorizationPrefix = "Bearer";
    
    // OAuth Endpoints
    public const string TokenEndpoint = "/token";
    public const string ValidateEndpoint = "/validate";
    
    // Query Parameters
    public const string UserLoginParam = "user_login";
    public const string UserIdParam = "user_id";
    public const string BroadcasterIdParam = "broadcaster_id";
    public const string FirstParam = "first";
    public const string GrantTypeParam = "grant_type";
    public const string ClientIdParam = "client_id";
    public const string ClientSecretParam = "client_secret";
    
    // OAuth Grant Types
    public const string ClientCredentialsGrantType = "client_credentials";
    
    // Configuration Keys (for appsettings.json)
    public const string ClientIdConfigKey = "Twitch:ClientId";
    public const string ClientSecretConfigKey = "Twitch:ClientSecret";
    
    // Default Values
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    
    // Rate Limiting
    public const int RateLimitPerMinute = 800;
    public const int RateLimitDelayMs = 100;
}