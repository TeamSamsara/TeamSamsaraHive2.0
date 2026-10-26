using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Configuration.Models.Enums;
using TeamSamsara.Hive.Bot.Configuration.Services;
using TeamSamsara.Hive.Bot.Integrations.Discord;
using TeamSamsara.Hive.Core.Abstractions;
using TeamSamsara.Hive.Core.EventBus.Events;

namespace TeamSamsara.Hive.Bot.Notifications;

// Sends stream live/offline notifications to the configured Discord channel
// Always builds messages via NotificationTemplates - never inline embeds here
public class StreamNotificationService : BaseService
{
    private readonly DiscordIntegration _discord;
    private readonly GetConfigValueService _configService;

    public override string ServiceName => "StreamNotification";

    public StreamNotificationService(
        DiscordIntegration discord,
        GetConfigValueService configService,
        ILogger<StreamNotificationService> logger,
        IConfiguration configuration)
        : base(logger, configuration)
    {
        _discord = discord ?? throw new ArgumentNullException(nameof(discord));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    public async Task SendStreamLiveNotificationAsync(StreamWentLiveEvent evt)
    {
        try
        {
            var channelId = await _configService.GetChannelIdAsync(ConfigKey.StreamNotificationChannelId);
            if (channelId == null)
            {
                LogWarning("StreamNotificationChannelId not configured - skipping live notification for {Username}", evt.Username);
                return;
            }

            // Per-stream role (set via /stream add or /stream set-role) wins
            // if present; otherwise fall back to the global StreamNotificationRoleId
            // config value. Either can be unset, in which case there's just no ping.
            var roleId = evt.NotificationRoleId
                ?? await _configService.GetRoleIdAsync(ConfigKey.StreamNotificationRoleId);

            var embed = NotificationTemplates.BuildStreamLiveEmbed(evt);
            var content = NotificationTemplates.BuildStreamLiveMessage(evt, roleId);
            await _discord.SendEmbedAsync(channelId.Value, embed, content);

            LogInfo("Live notification sent for {Username}", evt.Username);
        }
        catch (Exception ex)
        {
            // Rethrow so the event bus reports the failure and the stream
            // monitor retries the alert on its next poll
            LogError(ex, "Failed to send live notification for {Username}", evt.Username);
            throw;
        }
    }
}