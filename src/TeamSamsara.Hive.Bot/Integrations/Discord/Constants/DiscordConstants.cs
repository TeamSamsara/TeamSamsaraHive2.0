namespace TeamSamsara.Hive.Bot.Integrations.Discord.Constants;

// All string and numeric constants for Discord integration
public static class DiscordConstants
{
    // Configuration Keys (for appsettings.json)
    public const string BotTokenConfigKey = "Discord:BotToken";
    
    // Connection Watchdog Settings
    // Discord.Net reconnects on its own. The watchdog only steps in if the bot
    // has stayed disconnected this long, by stopping the app so the host
    // (Render) restarts it with a fresh connection.
    public const int DisconnectedRestartMinutes = 5;
    public const int WatchdogCheckIntervalSeconds = 30;
    
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
    public const string DisconnectedMessage = "Discord bot disconnected - Discord.Net will reconnect automatically";
    public const string WatchdogRestartMessage = "Discord has been disconnected for {Minutes:F1} minutes - stopping the app so the host restarts it";
    public const string ReadyTimeoutMessage = "Timed out waiting for Discord Ready event";
    public const string HealthCheckFailedMessage = "Discord health check failed - not connected";
}