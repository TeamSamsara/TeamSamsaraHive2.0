using Discord;
using TeamSamsara.Hive.Bot.Integrations.Discord.Constants;
using TeamSamsara.Hive.Core.EventBus.Events;

namespace TeamSamsara.Hive.Bot.Notifications;

// Single source of truth for every notification embed the bot sends.
// Every message shown to users should be built here - never inline an
// embed anywhere else. This is the only file that needs editing to change
// wording, and it guarantees every streamer gets the same format.
public static class NotificationTemplates
{
    #region Wording (edit here to change what gets sent)

    private const string LiveTitleFormat = "🔴 {0} is now live!";
    private const string LiveDescriptionFormat = "**{0}**";
    private const string LiveGameFieldName = "Playing";
    private const string LiveViewersFieldName = "Viewers";
    private const string LiveWatchFieldName = "Watch";
    private const string LiveWatchFieldFormat = "[Click here]({0})";

    private const string OfflineTitleFormat = "⚫ {0} has gone offline";
    private const string OfflineDescriptionFormat = "Stream ended after {0} minutes";
    private const string OfflinePeakFieldName = "Peak Viewers";
    private const string OfflineAvgFieldName = "Avg Viewers";

    #endregion

    // Build the embed sent when a tracked stream goes live
    public static Embed BuildStreamLiveEmbed(StreamWentLiveEvent evt)
    {
        var builder = new EmbedBuilder()
            .WithTitle(string.Format(LiveTitleFormat, evt.DisplayName))
            .WithDescription(string.Format(LiveDescriptionFormat, evt.StreamTitle))
            .WithUrl(evt.StreamUrl)
            .WithColor(DiscordConstants.SuccessEmbedColor)
            .WithTimestamp(evt.StartedAt)
            .AddField(LiveGameFieldName, string.IsNullOrEmpty(evt.GameName) ? "N/A" : evt.GameName, inline: true)
            .AddField(LiveViewersFieldName, evt.ViewerCount.ToString(), inline: true)
            .AddField(LiveWatchFieldName, string.Format(LiveWatchFieldFormat, evt.StreamUrl), inline: true);

        var thumbnail = FormatThumbnail(evt.ThumbnailUrl);
        if (!string.IsNullOrEmpty(thumbnail))
        {
            builder.WithThumbnailUrl(thumbnail);
        }

        return builder.Build();
    }

    // Build the embed sent when a tracked stream goes offline
    public static Embed BuildStreamOfflineEmbed(StreamWentOfflineEvent evt)
    {
        var builder = new EmbedBuilder()
            .WithTitle(string.Format(OfflineTitleFormat, evt.DisplayName))
            .WithDescription(string.Format(OfflineDescriptionFormat, evt.DurationMinutes))
            .WithColor(DiscordConstants.ErrorEmbedColor)
            .WithTimestamp(evt.EndedAt);

        if (evt.PeakViewers.HasValue)
        {
            builder.AddField(OfflinePeakFieldName, evt.PeakViewers.Value.ToString(), inline: true);
        }

        if (evt.AverageViewers.HasValue)
        {
            builder.AddField(OfflineAvgFieldName, evt.AverageViewers.Value.ToString(), inline: true);
        }

        return builder.Build();
    }

    // Twitch thumbnail URLs contain {width}x{height} placeholders that must be filled in
    private static string FormatThumbnail(string thumbnailUrlTemplate)
    {
        if (string.IsNullOrEmpty(thumbnailUrlTemplate)) return string.Empty;
        return thumbnailUrlTemplate.Replace("{width}", "440").Replace("{height}", "248");
    }
}