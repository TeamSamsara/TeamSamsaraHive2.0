using TeamSamsara.Hive.Core.Abstractions;
using TeamSamsara.Hive.Core.Workflows.Constants;
namespace TeamSamsara.Hive.Core.Workflows.Models;

// Represents a status transition event in a submission workflow
// Used for audit trail and status change history
public class StatusTransition : BaseModel
{
    #region Identification
    
    // ID of the submission this transition belongs to
    public string SubmissionId { get; set; } = string.Empty;
    
    // Type of submission (e.g., "ticket", "application")
    public string SubmissionType { get; set; } = string.Empty;
    
    #endregion
    
    #region Transition Details
    
    // Status before the transition
    public string FromStatus { get; set; } = string.Empty;
    
    // Status after the transition
    public string ToStatus { get; set; } = string.Empty;
    
    // When the transition occurred
    public DateTime TransitionedAtUtc { get; set; }
    
    #endregion
    
    #region Actor Information
    
    // Discord user who triggered the transition
    public ulong TransitionedByUserId { get; set; }
    
    // Username of the actor (for audit trail)
    public string TransitionedByUsername { get; set; } = string.Empty;
    
    #endregion
    
    #region Additional Context
    
    // Optional notes explaining the transition
    public string? Notes { get; set; }
    
    // Optional reason code (e.g., "user_cancelled", "staff_approved")
    public string? ReasonCode { get; set; }
    
    #endregion
    
    #region Helper Methods
    
    // Check if this is a terminal transition (moved to closed/completed/cancelled/rejected)
    public bool IsTerminalTransition()
    {
        return ToStatus == SubmissionStatuses.Closed
            || ToStatus == SubmissionStatuses.Completed
            || ToStatus == SubmissionStatuses.Cancelled
            || ToStatus == SubmissionStatuses.Rejected;
    }
    
    // Get a formatted description of the transition
    public string GetDescription()
    {
        var description = $"{FromStatus} → {ToStatus}";
        
        if (!string.IsNullOrWhiteSpace(Notes))
        {
            description += $": {Notes}";
        }
        
        return description;
    }
    
    #endregion
}