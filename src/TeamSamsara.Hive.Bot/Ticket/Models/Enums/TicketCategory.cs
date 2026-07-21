namespace TeamSamsara.Hive.Bot.Tickets.Models.Enums;

// Categories for ticket classification
// Helps staff route tickets to appropriate teams
public enum TicketCategory
{
    // General support or questions
    General = 0,
    
    // Bug reports or technical issues
    Bug = 1,
    
    // Feature requests or enhancements
    Feature = 2,
    
    // Questions or help requests
    Question = 3,
    
    // Account or access issues
    Account = 4,
    
    // Technical support issues
    Technical = 5,
    
    // Permission or role issues
    Permissions = 6,
    
    // Bot configuration or settings
    Configuration = 7,
    
    // Other uncategorized issues
    Other = 99
}