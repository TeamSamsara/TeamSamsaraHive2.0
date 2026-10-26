using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TeamSamsara.Hive.Core.EventBus;

// EventBus: Message board for orchestrator-to-orchestrator communication
// ONLY orchestrators can publish and subscribe
// Services never touch EventBus - they're workers, not communicators
public class EventBus
{
    // Thread-safe dictionary: EventType -> List of handler functions
    // Multiple orchestrators can subscribe to the same event type
    private readonly ConcurrentDictionary<Type, List<Delegate>> _subscribers = new();
    
    // Logger for tracking event flow (debugging cross-system communication)
    private readonly ILogger<EventBus> _logger;
    
    public EventBus(ILogger<EventBus> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    // Subscribe to an event type
    // Called by orchestrators in their constructor
    // Example: _eventBus.Subscribe<StreamWentLiveEvent>(OnStreamWentLive);
    public void Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class
    {
        var eventType = typeof(TEvent);
        
        // Add handler to the list for this event type
        _subscribers.AddOrUpdate(
            eventType,
            // If first subscriber: create new list with this handler
            _ => new List<Delegate> { handler },
            // If subscribers exist: add this handler to the list
            (_, handlers) =>
            {
                handlers.Add(handler);
                return handlers;
            });
        
        _logger.LogDebug("Subscribed to {EventType}. Total subscribers: {Count}", 
            eventType.Name, 
            _subscribers[eventType].Count);
    }
    
    // Publish an event to all subscribers
    // Called by orchestrators when something happens
    // Example: await _eventBus.PublishAsync(new StreamWentLiveEvent { ... });
    // Returns true when every subscriber handled it without throwing (also
    // true when nobody is subscribed), so a publisher can retry a failed
    // delivery. Callers that don't care can ignore the result.
    public async Task<bool> PublishAsync<TEvent>(TEvent eventData) where TEvent : class
    {
        var allSucceeded = true;
        var eventType = typeof(TEvent);
        
        // Check if anyone is listening
        if (!_subscribers.TryGetValue(eventType, out var handlers))
        {
            _logger.LogDebug("No subscribers for {EventType}", eventType.Name);
            return true;
        }
        
        _logger.LogInformation("Publishing {EventType} to {Count} subscriber(s)", 
            eventType.Name, 
            handlers.Count);
        
        // Call each subscriber's handler
        // ToList() creates a copy to avoid modification during iteration
        foreach (var handler in handlers.ToList())
        {
            try
            {
                // Cast to correct type and invoke
                if (handler is Func<TEvent, Task> asyncHandler)
                {
                    await asyncHandler(eventData);
                }
            }
            catch (Exception ex)
            {
                // Log error but continue calling other handlers
                // One broken subscriber shouldn't stop the others
                allSucceeded = false;
                _logger.LogError(ex, 
                    "Error in event handler for {EventType}", 
                    eventType.Name);
            }
        }

        return allSucceeded;
    }
}