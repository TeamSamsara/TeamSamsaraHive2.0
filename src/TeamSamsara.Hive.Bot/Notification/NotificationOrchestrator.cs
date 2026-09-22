using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Core.Abstractions;
using TeamSamsara.Hive.Core.EventBus;
using TeamSamsara.Hive.Core.EventBus.Events;

namespace TeamSamsara.Hive.Bot.Notifications;

// Root orchestrator for the Notification system.
// Only knows about events on the EventBus - has zero dependency on what
// publishes them. Works whether a lightweight stream watcher or a full
// Analytics system is what's raising StreamWentLive/Offline.
public class NotificationOrchestrator : BaseOrchestrator
{
    private readonly EventBus _eventBus;

    public override string SystemName => "Notifications";

    public NotificationOrchestrator(
        EventBus eventBus,
        ILogger<NotificationOrchestrator> logger,
        IServiceProvider serviceProvider)
        : base(logger, serviceProvider)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public override async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await base.InitializeAsync(cancellationToken);

        _eventBus.Subscribe<StreamWentLiveEvent>(OnStreamWentLive);

        LogInfo("Notification system subscribed to stream events");
    }

    private async Task OnStreamWentLive(StreamWentLiveEvent evt)
    {
        using var scope = ServiceProvider.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<StreamNotificationService>();
        await notificationService.SendStreamLiveNotificationAsync(evt);
    }
}