using TeamSamsara.Hive.Core.EventBus.Events;

namespace TeamSamsara.Hive.Core.EventBus.Events;

// Event published when an urgent priority ticket is created
// Published by: TicketOrchestrator
// Subscribed by: NotificationOrchestrator (for urgent notifications + role pings)
public class UrgentTicketCreatedEvent : BaseEvent
{
    // Ticket ID (e.g., "TKT-00001")
    public string TicketId { get; set; }
    
    // Ticket title/summary
    public string Title { get; set; }
    
    // Full ticket description
    public string Description { get; set; }
    
    // Ticket category (e.g., "Moderation", "Technical", "General")
    public string Category { get; set; }
    
    // Discord user ID who created the ticket
    public ulong CreatedByUserId { get; set; }
    
    // Discord username who created the ticket
    public string CreatedByUsername { get; set; }
    
    // When the ticket was created (UTC)
    public DateTime CreatedUtc { get; set; }
    
    // Discord channel ID for this ticket (the dedicated ticket channel)
    public ulong ChannelId { get; set; }
    
    // Constructor - sets SourceSystem to "Tickets"
    public UrgentTicketCreatedEvent() : base("Tickets")
    {
        // Required properties will be set by TicketOrchestrator
        TicketId = string.Empty;
        Title = string.Empty;
        Description = string.Empty;
        Category = string.Empty;
        CreatedByUsername = string.Empty;
    }
}