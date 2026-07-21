using TeamSamsara.Hive.Core.Workflows.Models;

namespace TeamSamsara.Hive.Core.Workflows.Abstractions;

// Interface for managing status transitions and state machine logic
// Implement this to define valid status transitions and transition rules
public interface IStatusTransition
{
    #region Validation
    
    // Check if a status transition is valid
    bool IsValidTransition(string fromStatus, string toStatus);
    
    // Get list of valid next statuses from current status
    List<string> GetValidNextStatuses(string currentStatus);
    
    // Check if status is a terminal state (cannot transition further)
    bool IsTerminalStatus(string status);
    
    #endregion
    
    #region Transition Recording
    
    // Record a status transition in the audit trail
    Task<StatusTransition> RecordTransitionAsync(
        string submissionId,
        string submissionType,
        string fromStatus,
        string toStatus,
        ulong transitionedByUserId,
        string transitionedByUsername,
        string? notes = null,
        string? reasonCode = null);
    
    // Get transition history for a submission
    Task<List<StatusTransition>> GetTransitionHistoryAsync(string submissionId);
    
    // Get the most recent transition for a submission
    Task<StatusTransition?> GetLatestTransitionAsync(string submissionId);
    
    #endregion
    
    #region Transition Rules
    
    // Check if a transition requires specific permissions
    bool RequiresPermission(string fromStatus, string toStatus);
    
    // Get required permission level for transition (if any)
    string? GetRequiredPermission(string fromStatus, string toStatus);
    
    #endregion
}