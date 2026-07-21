namespace TeamSamsara.Hive.Bot.Tickets.Models.Enums;

// Priority levels for tickets
// Determines urgency and response time expectations
public enum TicketPriority
{
    // Low priority - can wait, non-urgent
    Low = 1,
    
    // Medium priority - normal handling
    Medium = 2,
    
    // High priority - needs attention soon
    High = 3,
    
    // Critical priority - urgent, affecting multiple users
    Critical = 4,
    
    // Emergency - immediate attention required, system down
    Emergency = 5
}