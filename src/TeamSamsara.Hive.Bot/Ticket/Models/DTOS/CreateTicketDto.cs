using TeamSamsara.Hive.Bot.Tickets.Models.Enums;
using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Tickets.Models.DTOs;

// Data transfer object for creating a new ticket
// Populated from Discord modal form submission
public class CreateTicketDto : BaseDto
{
    #region Required Fields
    
    // Discord user creating the ticket
    public ulong SubmittedByUserId { get; set; }
    
    // Username at time of submission
    public string SubmittedByUsername { get; set; } = string.Empty;
    
    // Short title summarizing the issue
    public string Title { get; set; } = string.Empty;
    
    // Detailed description of the issue
    public string Description { get; set; } = string.Empty;
    
    #endregion
    
    #region Optional Fields
    
    // Category for classification (defaults to General)
    public TicketCategory Category { get; set; } = TicketCategory.General;
    
    // Priority level (defaults to Medium)
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    
    // Type for team routing (defaults to Support)
    public TicketType Type { get; set; } = TicketType.Support;
    
    #endregion
    
    #region Validation
    
    // Validate that all required fields are present
    public bool IsValid(out string? errorMessage)
    {
        if (SubmittedByUserId == 0)
        {
            errorMessage = "User ID is required";
            return false;
        }
        
        if (string.IsNullOrWhiteSpace(SubmittedByUsername))
        {
            errorMessage = "Username is required";
            return false;
        }
        
        if (string.IsNullOrWhiteSpace(Title))
        {
            errorMessage = "Title is required";
            return false;
        }
        
        if (Title.Length > 100)
        {
            errorMessage = "Title must be 100 characters or less";
            return false;
        }
        
        if (string.IsNullOrWhiteSpace(Description))
        {
            errorMessage = "Description is required";
            return false;
        }
        
        if (Description.Length > 2000)
        {
            errorMessage = "Description must be 2000 characters or less";
            return false;
        }
        
        errorMessage = null;
        return true;
    }
    
    #endregion
}