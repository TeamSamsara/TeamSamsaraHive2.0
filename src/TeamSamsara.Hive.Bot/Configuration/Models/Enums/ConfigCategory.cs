namespace TeamSamsara.Hive.Bot.Configuration.Models.Enums;

// Enum defining categories for organizing configuration keys
// Used to group related config keys together for display and management
public enum ConfigCategory
{
    NotificationChannels,    // All notification channel IDs (5 keys)
    Roles,                   // Discord role IDs (4 keys)
    TicketCategories,        // Discord categories for ticket system (2 keys)
    VoiceStatsChannels       // Voice channels displaying live stats (5 keys)
}