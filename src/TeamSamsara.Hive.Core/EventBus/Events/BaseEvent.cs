namespace TeamSamsara.Hive.Core.EventBus.Events;

// Base class for all Events
// Events are messages sent between orchestrators
// ONLY orchestrators can publish or subscribe to events

public abstract class BaseEvent
{
    // Unique identifier for this event instance
    public Guid EventId { get; }

    // When this event occurred (UTC)
    public DateTime TimestampUtc { get; }

    // Which system published this event (e.g., "Analytics", "Tickets", "Notifications")
    public string SourceSystem { get; set; }

    // Constructor
    protected BaseEvent(string sourceSystem)
    {
        EventId = Guid.NewGuid();
        TimestampUtc = DateTime.UtcNow;
        SourceSystem = sourceSystem;
    }
}