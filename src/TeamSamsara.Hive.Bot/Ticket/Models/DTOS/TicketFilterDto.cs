using TeamSamsara.Hive.Bot.Tickets.Models.Enums;
using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Tickets.Models.DTOs;

// Data transfer object for filtering tickets in queries
// All fields are optional - only provided fields will filter results
public class TicketFilterDto : BaseDto
{
    #region Filter Criteria
    
    // Filter by status (null = all statuses)
    public string? Status { get; set; }
    
    // Filter by category (null = all categories)
    public TicketCategory? Category { get; set; }
    
    // Filter by priority (null = all priorities)
    public TicketPriority? Priority { get; set; }
    
    // Filter by type (null = all types)
    public TicketType? Type { get; set; }
    
    // Filter by submitted by user (null = all users)
    public ulong? SubmittedByUserId { get; set; }
    
    // Filter by assigned to user (null = all assignments)
    public ulong? AssignedToUserId { get; set; }
    
    // Filter by minimum priority (null = no minimum)
    public TicketPriority? MinimumPriority { get; set; }
    
    #endregion
    
    #region Sorting & Pagination
    
    // Sort field (default: SubmittedAtUtc)
    public string SortBy { get; set; } = "SubmittedAtUtc";
    
    // Sort direction (default: descending = newest first)
    public bool SortDescending { get; set; } = true;
    
    // Page number for pagination (1-indexed)
    public int Page { get; set; } = 1;
    
    // Items per page (default: 10, max: 50)
    public int PageSize { get; set; } = 10;
    
    #endregion
    
    #region Helper Methods
    
    // Check if any filters are applied
    public bool HasFilters()
    {
        return Status != null 
            || Category.HasValue 
            || Priority.HasValue 
            || Type.HasValue
            || SubmittedByUserId.HasValue
            || AssignedToUserId.HasValue
            || MinimumPriority.HasValue;
    }
    
    // Validate pagination parameters
    public bool IsValid(out string? errorMessage)
    {
        if (Page < 1)
        {
            errorMessage = "Page must be 1 or greater";
            return false;
        }
        
        if (PageSize < 1)
        {
            errorMessage = "Page size must be 1 or greater";
            return false;
        }
        
        if (PageSize > 50)
        {
            errorMessage = "Page size cannot exceed 50";
            return false;
        }
        
        errorMessage = null;
        return true;
    }
    
    // Get skip count for pagination
    public int GetSkip()
    {
        return (Page - 1) * PageSize;
    }
    
    // Get take count for pagination
    public int GetTake()
    {
        return PageSize;
    }
    
    // Get a summary of active filters for display
    public string GetFilterSummary()
    {
        var filters = new List<string>();
        
        if (Status != null)
            filters.Add($"Status: {Status}");
        
        if (Category.HasValue)
            filters.Add($"Category: {Category}");
        
        if (Priority.HasValue)
            filters.Add($"Priority: {Priority}");
        
        if (Type.HasValue)
            filters.Add($"Type: {Type}");
        
        if (SubmittedByUserId.HasValue)
            filters.Add($"Submitted by user: {SubmittedByUserId}");
        
        if (AssignedToUserId.HasValue)
            filters.Add($"Assigned to user: {AssignedToUserId}");
        
        if (MinimumPriority.HasValue)
            filters.Add($"Min Priority: {MinimumPriority}");
        
        return filters.Any() ? string.Join(", ", filters) : "No filters applied";
    }
    
    #endregion
}