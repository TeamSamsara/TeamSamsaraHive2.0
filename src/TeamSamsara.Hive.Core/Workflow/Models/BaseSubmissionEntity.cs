using TeamSamsara.Hive.Core.Abstractions;
using TeamSamsara.Hive.Core.Workflows.Constants;

namespace TeamSamsara.Hive.Core.Workflows.Abstractions;

// Base entity for all submission-based workflows (tickets, applications, forms, etc.)
// Provides common fields for status tracking, assignment, and lifecycle management
// Extend this class to create specific submission types (TicketEntity, ApplicantEntity, etc.)
public abstract class BaseSubmissionEntity : BaseEntity
{
    #region Identification
    
    // Unique identifier for this submission (e.g., "TICKET-001", "APP-123")
    // Format determined by subclass
    public string SubmissionId { get; set; } = string.Empty;
    
    // Discord user who submitted this
    public ulong SubmittedByUserId { get; set; }
    
    // Username at time of submission (for audit trail)
    public string SubmittedByUsername { get; set; } = string.Empty;
    
    #endregion
    
    #region Status Workflow
    
    // Current status in the workflow
    // Use constants from SubmissionStatuses or subclass-specific status constants
    // Stored as string for flexibility across different workflow types
    public string Status { get; set; } = SubmissionStatuses.Pending;
    
    // When the submission was initially created
    public DateTime SubmittedAtUtc { get; set; }
    
    #endregion
    
    #region Assignment Tracking
    
    // Discord user assigned to handle this submission (staff member, reviewer, etc.)
    // Null if not yet assigned
    public ulong? AssignedToUserId { get; set; }
    
    // Username of assigned user (for audit trail)
    public string? AssignedToUsername { get; set; }
    
    // When the assignment was made
    public DateTime? AssignedAtUtc { get; set; }
    
    #endregion
    
    #region Resolution Tracking
    
    // When the submission was marked as resolved/completed
    // Null if not yet resolved
    public DateTime? ResolvedAtUtc { get; set; }
    
    // When the submission was finally closed
    // Null if not yet closed
    public DateTime? ClosedAtUtc { get; set; }
    
    #endregion
    
    #region Metadata
    
    // Internal staff notes not visible to submitter
    // Used for decision rationale, context, etc.
    public string? StaffNotes { get; set; }
    
    #endregion
    
    #region Helper Methods
    
    // Check if submission is in a terminal state (closed, completed, cancelled, or rejected)
    public bool IsTerminalState()
    {
        return Status == SubmissionStatuses.Closed 
            || Status == SubmissionStatuses.Completed
            || Status == SubmissionStatuses.Cancelled
            || Status == SubmissionStatuses.Rejected;
    }
    
    // Check if submission is in a terminal state
    public bool IsClosed()
    {
        return ClosedAtUtc.HasValue;
    }
    
    // Check if submission is assigned to someone
    public bool IsAssigned()
    {
        return AssignedToUserId.HasValue;
    }
    
    // Check if submission is resolved
    public bool IsResolved()
    {
        return ResolvedAtUtc.HasValue;
    }
    
    // Get how long submission has been open (in days)
    public int GetDaysOpen()
    {
        var endTime = ClosedAtUtc ?? DateTime.UtcNow;
        return (int)(endTime - SubmittedAtUtc).TotalDays;
    }
    
    #endregion
}