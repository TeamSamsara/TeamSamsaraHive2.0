using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Integrations.Twitch;
using TeamSamsara.Hive.Bot.Streams.Repositories;
using TeamSamsara.Hive.Core.EventBus;
using TeamSamsara.Hive.Core.EventBus.Events;

namespace TeamSamsara.Hive.Bot.Streams.Services;

// Minimal, standalone stream watcher. Polls Twitch for tracked streamers and
// publishes StreamWentLiveEvent/StreamWentOfflineEvent when state changes.
//
// Intentionally has NO dependency on the Notification system, and does NOT
// require the full Analytics system (snapshots, viewer sampling, reporting).
// It only detects state changes and publishes events - anything downstream
// (Notifications today, full Analytics later) just subscribes to those events.
public class StreamMonitorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly EventBus _eventBus;
    private readonly ILogger<StreamMonitorService> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(60);

    // Gap enforced between consecutive "went live"/"went offline" notifications
    // within the SAME poll cycle. Without this, if several tracked streams all
    // transition state in the same 60s tick (e.g. right after a restart), every
    // notification fires back-to-back with zero gap between messages.
    private readonly TimeSpan _notificationStaggerDelay = TimeSpan.FromSeconds(5);

    // In-memory record of when each live stream started, used to compute
    // duration when it goes offline. Fine to lose on restart - non-critical.
    private readonly Dictionary<string, DateTime> _liveSince = new();

    public StreamMonitorService(
        IServiceProvider serviceProvider,
        EventBus eventBus,
        ILogger<StreamMonitorService> logger)
    {
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[StreamMonitor] Starting - polling every {Seconds}s", _pollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAllStreamsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[StreamMonitor] Error during poll cycle");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task PollAllStreamsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IStreamRepository>();
        var twitch = scope.ServiceProvider.GetRequiredService<TwitchIntegration>();

        var trackedStreams = await repository.GetAllAsync();

        foreach (var stream in trackedStreams)
        {
            var status = await twitch.GetStreamStatusAsync(stream.TwitchUsername);
            var isLiveNow = status != null;

            if (isLiveNow && !stream.IsLive)
            {
                stream.IsLive = true;
                stream.LastLiveAtUtc = DateTime.UtcNow;
                stream.LastStreamTitle = status!.title;
                stream.LastGameName = status.game_name;
                await repository.UpdateAsync(stream);

                _liveSince[stream.TwitchUsername] = status.started_at;

                await _eventBus.PublishAsync(new StreamWentLiveEvent
                {
                    Username = stream.TwitchUsername,
                    DisplayName = stream.DisplayName,
                    StreamTitle = status.title,
                    GameName = status.game_name,
                    ViewerCount = status.viewer_count,
                    StreamUrl = $"https://twitch.tv/{stream.TwitchUsername}",
                    ThumbnailUrl = status.thumbnail_url,
                    StartedAt = status.started_at,
                    NotificationRoleId = stream.NotificationRoleId
                });

                _logger.LogInformation("[StreamMonitor] {Username} went live", stream.TwitchUsername);

                await Task.Delay(_notificationStaggerDelay, stoppingToken);
            }
            else if (!isLiveNow && stream.IsLive)
            {
                stream.IsLive = false;
                stream.LastOfflineAtUtc = DateTime.UtcNow;
                await repository.UpdateAsync(stream);

                var startedAt = _liveSince.TryGetValue(stream.TwitchUsername, out var started) ? started : DateTime.UtcNow;
                var duration = (int)(DateTime.UtcNow - startedAt).TotalMinutes;
                _liveSince.Remove(stream.TwitchUsername);

                await _eventBus.PublishAsync(new StreamWentOfflineEvent
                {
                    Username = stream.TwitchUsername,
                    DisplayName = stream.DisplayName,
                    EndedAt = DateTime.UtcNow,
                    DurationMinutes = Math.Max(duration, 0)
                });

                _logger.LogInformation("[StreamMonitor] {Username} went offline", stream.TwitchUsername);

                await Task.Delay(_notificationStaggerDelay, stoppingToken);
            }
        }
    }
}