using TeamSamsara.Hive.Core.EventBus.Events;

namespace TeamSamsara.Hive.Core.EventBus.Events;

// Event published when a ticket is closed/resolved
// Published by: TicketOrchestrator
// Subscribed by: NotificationOrchestrator (optional - for logging/metrics)
public class TicketClosedEvent : BaseEvent
{
    // Ticket ID (e.g., "TKT-00001")
    public string TicketId { get; set; }
    
    // Ticket title/summary
    public string Title { get; set; }
    
    // Discord user ID of person who closed the ticket
    public ulong ClosedByUserId { get; set; }
    
    // Discord username of person who closed the ticket
    public string ClosedByUsername { get; set; }
    
    // When the ticket was closed (UTC)
    public DateTime ClosedUtc { get; set; }
    
    // How the ticket was resolved (e.g., "Resolved", "Duplicate", "CannotReproduce", "WontFix")
    public string ResolutionType { get; set; }
    
    // Resolution notes/comments (nullable)
    public string? ResolutionNotes { get; set; }
    
    // Time taken to resolve (in minutes)
    public int ResolutionTimeMinutes { get; set; }
    
    // Discord channel ID for this ticket
    public ulong ChannelId { get; set; }
    
    // Constructor - sets SourceSystem to "Tickets"
    public TicketClosedEvent() : base("Tickets")
    {
        // Required properties will be set by TicketOrchestrator
        TicketId = string.Empty;
        Title = string.Empty;
        ClosedByUsername = string.Empty;
        ResolutionType = string.Empty;
    }
}