namespace TeamSamsara.Hive.Bot.Integrations.Discord.Constants;

// All string and numeric constants for Discord integration
public static class DiscordConstants
{
    // Configuration Keys (for appsettings.json)
    public const string BotTokenConfigKey = "Discord:BotToken";
    
    // Reconnection Settings
    public const int MaxReconnectAttempts = 5;
    public const int InitialReconnectDelayMs = 1000;      // 1 second
    public const int MaxReconnectDelayMs = 16000;         // 16 seconds
    public const double ReconnectBackoffMultiplier = 2.0; // Exponential backoff
    
    // Timeout Settings
    public const int ReadyTimeoutSeconds = 30;            // Max wait for Ready event
    
    // Embed Settings
    public const uint DefaultEmbedColor = 0x5865F2;       // Discord Blurple
    public const uint SuccessEmbedColor = 0x57F287;       // Green
    public const uint ErrorEmbedColor = 0xED4245;         // Red
    public const uint WarningEmbedColor = 0xFEE75C;       // Yellow
    
    // Log Messages
    public const string LogPrefix = "[DiscordIntegration]";
    public const string InitializingMessage = "Initializing Discord integration...";
    public const string ConnectedMessage = "Discord bot connected successfully";
    public const string DisconnectedMessage = "Discord bot disconnected";
    public const string ReconnectingMessage = "Attempting to reconnect to Discord (Attempt {0}/{1})";
    public const string ReconnectSuccessMessage = "Successfully reconnected to Discord";
    public const string ReconnectFailedMessage = "Failed to reconnect to Discord after {0} attempts";
    public const string ReadyTimeoutMessage = "Timed out waiting for Discord Ready event";
    public const string HealthCheckFailedMessage = "Discord health check failed - not connected";
}