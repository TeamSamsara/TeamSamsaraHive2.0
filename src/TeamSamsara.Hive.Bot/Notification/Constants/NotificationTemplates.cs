using Discord;
using TeamSamsara.Hive.Bot.Integrations.Discord.Constants;
using TeamSamsara.Hive.Bot.Streams.Models.Entities;
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

    // The actual Discord message content (not the embed) sent when a stream
    // goes live. {0} = role ping (or empty if none configured/overridden),
    // {1} = display name, {2} = stream URL.
    private const string LiveMessageFormat = "{0}**{1}** vient de passer en live ! [Regarder sur Twitch]({2})";

    private const string ListTitle = "Tracked Streams";
    private const string ListEmptyDescription = "No streams are currently being tracked. Use `/stream add` to start tracking one.";
    private const string ListLineFormat = "{0} **{1}** (`{2}`) — {3}";
    private const string ListDefaultRoleLabel = "_default role_";

    #endregion

    // Build the plain-text message content sent alongside the live embed.
    // If roleId is provided, the role is pinged at the front of the message.
    // Pass null when no role (per-stream or global) is configured - the
    // message still gets sent, just without a ping.
    public static string BuildStreamLiveMessage(StreamWentLiveEvent evt, ulong? roleId)
    {
        var rolePing = roleId.HasValue ? $"<@&{roleId.Value}> " : string.Empty;
        return string.Format(LiveMessageFormat, rolePing, evt.DisplayName, evt.StreamUrl);
    }

    // Build the embed listing every currently tracked streamer, their live
    // status, and which role (if any) is overridden for them specifically.
    public static Embed BuildStreamListEmbed(IReadOnlyList<TrackedStreamEntity> streams)
    {
        var builder = new EmbedBuilder()
            .WithTitle(ListTitle)
            .WithColor(DiscordConstants.SuccessEmbedColor)
            .WithCurrentTimestamp();

        if (streams.Count == 0)
        {
            builder.WithDescription(ListEmptyDescription);
            return builder.Build();
        }

        var lines = streams.Select(s =>
        {
            var statusIcon = s.IsLive ? "🔴" : "⚫";
            var roleLabel = s.NotificationRoleId.HasValue ? $"<@&{s.NotificationRoleId.Value}>" : ListDefaultRoleLabel;
            return string.Format(ListLineFormat, statusIcon, s.DisplayName, s.TwitchUsername, roleLabel);
        });

        builder.WithDescription(string.Join("\n", lines));

        return builder.Build();
    }

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

    // Twitch thumbnail URLs contain {width}x{height} placeholders that must be filled in
    private static string FormatThumbnail(string thumbnailUrlTemplate)
    {
        if (string.IsNullOrEmpty(thumbnailUrlTemplate)) return string.Empty;
        return thumbnailUrlTemplate.Replace("{width}", "440").Replace("{height}", "248");
    }
}