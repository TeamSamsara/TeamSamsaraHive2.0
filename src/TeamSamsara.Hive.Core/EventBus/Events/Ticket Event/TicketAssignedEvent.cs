using TeamSamsara.Hive.Core.EventBus.Events;

namespace TeamSamsara.Hive.Core.EventBus.Events;

// Event published when a ticket is assigned to a staff member
// Published by: TicketOrchestrator
// Subscribed by: NotificationOrchestrator (for DM to assigned staff)
public class TicketAssignedEvent : BaseEvent
{
    // Ticket ID (e.g., "TKT-00001")
    public string TicketId { get; set; }
    
    // Ticket title/summary
    public string Title { get; set; }
    
    // Ticket priority (e.g., "Urgent", "High", "Normal", "Low")
    public string Priority { get; set; }
    
    // Ticket category (e.g., "Moderation", "Technical", "General")
    public string Category { get; set; }
    
    // Discord user ID of staff member assigned to ticket
    public ulong AssignedToUserId { get; set; }
    
    // Discord username of staff member assigned
    public string AssignedToUsername { get; set; }
    
    // Discord user ID of person who assigned the ticket
    public ulong AssignedByUserId { get; set; }
    
    // When the ticket was assigned (UTC)
    public DateTime AssignedUtc { get; set; }
    
    // Due date for ticket (nullable - may not be set)
    public DateTime? DueUtc { get; set; }
    
    // Discord channel ID for this ticket
    public ulong ChannelId { get; set; }
    
    // Constructor - sets SourceSystem to "Tickets"
    public TicketAssignedEvent() : base("Tickets")
    {
        // Required properties will be set by TicketOrchestrator
        TicketId = string.Empty;
        Title = string.Empty;
        Priority = string.Empty;
        Category = string.Empty;
        AssignedToUsername = string.Empty;
    }
}