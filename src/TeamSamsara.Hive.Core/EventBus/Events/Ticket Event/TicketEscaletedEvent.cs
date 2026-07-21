using TeamSamsara.Hive.Core.EventBus.Events;

namespace TeamSamsara.Hive.Core.EventBus.Events;

// Event published when a ticket priority is escalated
// Published by: TicketOrchestrator
// Subscribed by: NotificationOrchestrator (for escalation alerts to admins)
public class TicketEscalatedEvent : BaseEvent
{
    // Ticket ID (e.g., "TKT-00001")
    public string TicketId { get; set; }
    
    // Ticket title/summary
    public string Title { get; set; }
    
    // Previous priority level (e.g., "High", "Normal")
    public string OldPriority { get; set; }
    
    // New priority level (always higher - e.g., "Urgent", "High")
    public string NewPriority { get; set; }
    
    // Why the ticket was escalated (e.g., "NoResponse", "Manual", "UserRequest")
    public string EscalationReason { get; set; }
    
    // How long the ticket was waiting before escalation (in minutes)
    public int WaitTimeMinutes { get; set; }
    
    // When the ticket was escalated (UTC)
    public DateTime EscalatedUtc { get; set; }
    
    // Discord channel ID for this ticket
    public ulong ChannelId { get; set; }
    
    // Constructor - sets SourceSystem to "Tickets"
    public TicketEscalatedEvent() : base("Tickets")
    {
        // Required properties will be set by TicketOrchestrator
        TicketId = string.Empty;
        Title = string.Empty;
        OldPriority = string.Empty;
        NewPriority = string.Empty;
        EscalationReason = string.Empty;
    }
}