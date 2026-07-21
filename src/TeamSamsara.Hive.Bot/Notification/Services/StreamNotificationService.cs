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

            var embed = NotificationTemplates.BuildStreamLiveEmbed(evt);
            await _discord.SendEmbedAsync(channelId.Value, embed);

            LogInfo("Live notification sent for {Username}", evt.Username);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to send live notification for {Username}", evt.Username);
        }
    }

    public async Task SendStreamOfflineNotificationAsync(StreamWentOfflineEvent evt)
    {
        try
        {
            var channelId = await _configService.GetChannelIdAsync(ConfigKey.StreamNotificationChannelId);
            if (channelId == null)
            {
                LogWarning("StreamNotificationChannelId not configured - skipping offline notification for {Username}", evt.Username);
                return;
            }

            var embed = NotificationTemplates.BuildStreamOfflineEmbed(evt);
            await _discord.SendEmbedAsync(channelId.Value, embed);

            LogInfo("Offline notification sent for {Username}", evt.Username);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to send offline notification for {Username}", evt.Username);
        }
    }
}