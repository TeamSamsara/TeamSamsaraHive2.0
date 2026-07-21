namespace TeamSamsara.Hive.Bot.Tickets.Models.Enums;

// Types of tickets for routing to different teams
// Determines which team handles the ticket
public enum TicketType
{
    // General support ticket
    Support = 0,
    
    // Technical or bot-related issue
    Technical = 1,
    
    // Community or moderation issue
    Community = 2,
    
    // Administrative or account issue
    Administrative = 3,
    
    // Feedback or suggestion
    Feedback = 4
}