namespace TeamSamsara.Hive.Bot.Configuration.Models.Enums;

// Enum defining all configuration keys based on appsettings.json Discord section
// These map to Discord IDs that can be dynamically configured via /config commands
// Total: 16 keys across 4 categories
public enum ConfigKey
{
    #region Notification Channels (5)
    
    StreamNotificationChannelId,     // Discord.NotificationChannels.StreamNotificationChannelId
    UrgentTicketChannelId,           // Discord.NotificationChannels.UrgentTicketChannelId
    AdminChannelId,                  // Discord.NotificationChannels.AdminChannelId
    AnalyticsReportChannelId,        // Discord.NotificationChannels.AnalyticsReportChannelId
    TicketLogChannelId,              // Discord.NotificationChannels.TicketLogChannelId
    TicketEmbedChannelId,            // Discord.NotificationChannels.TicketEmbedChanelId
    
    #endregion
    
    #region Roles (4)
    
    AdminRoleId,                     // Discord.Roles.AdminRoleId
    ModeratorRoleId,                 // Discord.Roles.ModeratorRoleId
    StaffRoleId,                     // Discord.Roles.StaffRoleId
    StreamNotificationRoleId,        // Discord.Roles.StreamNotificationRoleId
    
    #endregion
    
    #region Ticket Categories (2)
    
    ActiveTicketsCategoryId,         // TicketSettings.DiscordChannels.ActiveTicketsCategoryId
    ClosedTicketsCategoryId,         // TicketSettings.DiscordChannels.ClosedTicketsCategoryId
    
    #endregion
    
    #region Voice Stats Channels (5)
    
    UrgentStatsChannelId,            // TicketSettings.DiscordChannels.VoiceStatsChannels.UrgentStatsChannelId
    HighStatsChannelId,              // TicketSettings.DiscordChannels.VoiceStatsChannels.HighStatsChannelId
    NormalStatsChannelId,            // TicketSettings.DiscordChannels.VoiceStatsChannels.NormalStatsChannelId
    EscalatedStatsChannelId,         // TicketSettings.DiscordChannels.VoiceStatsChannels.EscalatedStatsChannelId
    OldestStatsChannelId,             // TicketSettings.DiscordChannels.VoiceStatsChannels.OldestStatsChannelId
    
    #endregion

    #region Feature Flags (1)
    TicketsEnabled // TicketSettings.TicketsEnabled
    #endregion
}