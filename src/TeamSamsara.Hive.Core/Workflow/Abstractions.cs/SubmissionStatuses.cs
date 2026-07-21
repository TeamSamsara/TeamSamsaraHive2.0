namespace TeamSamsara.Hive.Core.Workflows.Constants;

// Common status constants used across all submission-based workflows
// Subclasses can define additional status constants specific to their workflow
public static class SubmissionStatuses
{
    // Initial state, awaiting review
    public const string Pending = "pending";
    
    // Assigned to someone
    public const string Assigned = "assigned";
    
    // Being worked on
    public const string InProgress = "in_progress";
    
    // Solution provided
    public const string Resolved = "resolved";
    
    // Successfully finished
    public const string Completed = "completed";
    
    // Archived/finished
    public const string Closed = "closed";
    
    // User cancelled
    public const string Cancelled = "cancelled";
    
    // Denied/rejected
    public const string Rejected = "rejected";
}