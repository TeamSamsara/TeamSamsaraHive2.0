using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Integrations.Twitch;
using TeamSamsara.Hive.Bot.Streams.Models.Entities;
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

    // Failed live-alert attempts per streamer (in memory). After
    // MaxLiveAlertAttempts failures in a row the stream is marked live anyway,
    // so a permanently broken channel doesn't retry and log every minute.
    private readonly Dictionary<string, int> _liveAlertFailures = new();
    private const int MaxLiveAlertAttempts = 5;

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
            // Each streamer is checked on its own: one renamed or banned
            // account (or a Twitch hiccup for it) no longer stops the others.
            try
            {
                await PollStreamAsync(stream, repository, twitch, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[StreamMonitor] Failed to check {Username}", stream.TwitchUsername);
            }
        }
    }

    private async Task PollStreamAsync(
        TrackedStreamEntity stream,
        IStreamRepository repository,
        TwitchIntegration twitch,
        CancellationToken stoppingToken)
    {
        var status = await twitch.GetStreamStatusAsync(stream.TwitchUsername);
        var isLiveNow = status != null;

        if (isLiveNow && !stream.IsLive)
        {
            _liveSince[stream.TwitchUsername] = status!.started_at;

            // Announce FIRST, and only save "live" once the announcement went
            // out. Before, the stream was saved as live up front, so a failed
            // post (e.g. missing channel permissions) meant that stream was
            // never announced at all. Now a failed post is retried on the next
            // poll, up to MaxLiveAlertAttempts times.
            var delivered = await _eventBus.PublishAsync(new StreamWentLiveEvent
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

            if (!delivered)
            {
                var attempts = _liveAlertFailures.TryGetValue(stream.TwitchUsername, out var failed) ? failed + 1 : 1;
                _liveAlertFailures[stream.TwitchUsername] = attempts;

                if (attempts < MaxLiveAlertAttempts)
                {
                    _logger.LogWarning("[StreamMonitor] Live alert for {Username} failed (attempt {Attempt}/{Max}); retrying next poll",
                        stream.TwitchUsername, attempts, MaxLiveAlertAttempts);
                    return;
                }

                _logger.LogError("[StreamMonitor] Live alert for {Username} failed {Max} times; giving up for this stream",
                    stream.TwitchUsername, MaxLiveAlertAttempts);
            }

            _liveAlertFailures.Remove(stream.TwitchUsername);

            stream.IsLive = true;
            stream.LastLiveAtUtc = status.started_at;
            stream.LastStreamTitle = status.title;
            stream.LastGameName = status.game_name;
            await repository.UpdateAsync(stream);

            _logger.LogInformation("[StreamMonitor] {Username} went live", stream.TwitchUsername);

            await Task.Delay(_notificationStaggerDelay, stoppingToken);
        }
        else if (!isLiveNow && stream.IsLive)
        {
            stream.IsLive = false;
            stream.LastOfflineAtUtc = DateTime.UtcNow;
            await repository.UpdateAsync(stream);

            // _liveSince is lost on restart; fall back to the saved go-live time
            var startedAt = _liveSince.TryGetValue(stream.TwitchUsername, out var started)
                ? started
                : stream.LastLiveAtUtc ?? DateTime.UtcNow;
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
        else if (!isLiveNow)
        {
            // Went offline before an alert could be delivered: start fresh next time
            _liveAlertFailures.Remove(stream.TwitchUsername);
        }
    }
}