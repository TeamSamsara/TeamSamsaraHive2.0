using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Core.EventBus.Events;

namespace TeamSamsara.Hive.Core.EventBus;

// Router: Optional helper for routing events with additional logging
// Thin wrapper around EventBus that logs event routing
// Not strictly necessary, but useful for debugging cross-system communication
public class Router
{
    private readonly EventBus _eventBus;
    private readonly ILogger<Router> _logger;
    
    public Router(EventBus eventBus, ILogger<Router> logger)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    // Route an event through the EventBus
    // Provides additional logging for audit trail
    public async Task RouteEventAsync<TEvent>(TEvent eventData) where TEvent : BaseEvent
    {
        _logger.LogInformation(
            "Routing event {EventType} from {SourceSystem} (EventId: {EventId})", 
            typeof(TEvent).Name, 
            eventData.SourceSystem,
            eventData.EventId);
        
        // Forward to EventBus - subscribers will handle it
        await _eventBus.PublishAsync(eventData);
        
        _logger.LogDebug("Event {EventId} routing complete", eventData.EventId);
    }
}