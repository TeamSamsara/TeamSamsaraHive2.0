using TeamSamsara.Hive.Bot.Tickets.Models.Enums;
using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Tickets.Models.DTOs;

// Data transfer object for updating an existing ticket
// All fields are optional - only provided fields will be updated
public class UpdateTicketDto : BaseDto
{
    #region Identification
    
    // Ticket ID to update (required)
    public string TicketId { get; set; } = string.Empty;
    
    // User making the update
    public ulong UpdatedByUserId { get; set; }
    
    // Username of updater (for audit trail)
    public string UpdatedByUsername { get; set; } = string.Empty;
    
    #endregion
    
    #region Optional Update Fields
    
    // Update title (null = don't update)
    public string? Title { get; set; }
    
    // Update description (null = don't update)
    public string? Description { get; set; }
    
    // Update category (null = don't update)
    public TicketCategory? Category { get; set; }
    
    // Update priority (null = don't update)
    public TicketPriority? Priority { get; set; }
    
    // Update type (null = don't update)
    public TicketType? Type { get; set; }
    
    // Update status (null = don't update)
    public string? Status { get; set; }
    
    // Add/update resolution notes (null = don't update)
    public string? Resolution { get; set; }
    
    // Add staff notes (null = don't update)
    public string? StaffNotes { get; set; }
    
    #endregion
    
    #region Validation
    
    // Validate the update request
    public bool IsValid(out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(TicketId))
        {
            errorMessage = "Ticket ID is required";
            return false;
        }
        
        if (UpdatedByUserId == 0)
        {
            errorMessage = "Updated by user ID is required";
            return false;
        }
        
        if (string.IsNullOrWhiteSpace(UpdatedByUsername))
        {
            errorMessage = "Updated by username is required";
            return false;
        }
        
        // Validate title length if provided
        if (Title != null && Title.Length > 100)
        {
            errorMessage = "Title must be 100 characters or less";
            return false;
        }
        
        // Validate description length if provided
        if (Description != null && Description.Length > 2000)
        {
            errorMessage = "Description must be 2000 characters or less";
            return false;
        }
        
        // At least one field must be updated
        if (Title == null && Description == null && Category == null && 
            Priority == null && Type == null && Status == null && 
            Resolution == null && StaffNotes == null)
        {
            errorMessage = "At least one field must be updated";
            return false;
        }
        
        errorMessage = null;
        return true;
    }
    
    // Check if a specific field is being updated
    public bool IsUpdating(string fieldName)
    {
        return fieldName switch
        {
            nameof(Title) => Title != null,
            nameof(Description) => Description != null,
            nameof(Category) => Category.HasValue,
            nameof(Priority) => Priority.HasValue,
            nameof(Type) => Type.HasValue,
            nameof(Status) => Status != null,
            nameof(Resolution) => Resolution != null,
            nameof(StaffNotes) => StaffNotes != null,
            _ => false
        };
    }
    
    #endregion
}