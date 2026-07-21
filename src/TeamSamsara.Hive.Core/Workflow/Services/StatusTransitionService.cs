using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Core.Abstractions;
using TeamSamsara.Hive.Core.Workflows.Abstractions;
using TeamSamsara.Hive.Core.Workflows.Constants;
using TeamSamsara.Hive.Core.Workflows.Models;

namespace TeamSamsara.Hive.Core.Workflows.Services;

// Base implementation of status transition logic
// Subclasses can override to define system-specific state machine rules
public abstract class StatusTransitionService : BaseService, IStatusTransition
{
    #region Constructor
    
    protected StatusTransitionService(ILogger logger, IConfiguration configuration) 
        : base(logger, configuration)
    {
    }
    
    #endregion
    
    #region Abstract Methods - Subclasses Must Implement
    
    // Save transition to audit trail (database/storage)
    protected abstract Task<StatusTransition> SaveTransitionAsync(StatusTransition transition);
    
    // Get transition history from storage
    protected abstract Task<List<StatusTransition>> GetTransitionsFromStorageAsync(string submissionId);
    
    #endregion
    
    #region Validation
    
    // Default implementation allows any transition
    // Subclasses override to implement strict state machine rules
    public virtual bool IsValidTransition(string fromStatus, string toStatus)
    {
        // Cannot transition to the same status
        if (fromStatus == toStatus)
        {
            return false;
        }
        
        // Cannot transition from terminal states
        if (IsTerminalStatus(fromStatus))
        {
            return false;
        }
        
        // All other transitions allowed by default
        // Subclasses can implement strict rules
        return true;
    }
    
    // Get valid next statuses from current status
    // Default implementation returns common statuses
    // Subclasses override for specific state machine
    public virtual List<string> GetValidNextStatuses(string currentStatus)
    {
        // Cannot transition from terminal states
        if (IsTerminalStatus(currentStatus))
        {
            return new List<string>();
        }
        
        // Default: return all common statuses except current
        var allStatuses = new List<string>
        {
            SubmissionStatuses.Pending,
            SubmissionStatuses.Assigned,
            SubmissionStatuses.InProgress,
            SubmissionStatuses.Resolved,
            SubmissionStatuses.Completed,
            SubmissionStatuses.Closed,
            SubmissionStatuses.Cancelled,
            SubmissionStatuses.Rejected
        };
        
        return allStatuses.Where(s => s != currentStatus).ToList();
    }
    
    // Check if status is terminal (no further transitions)
    public virtual bool IsTerminalStatus(string status)
    {
        return status == SubmissionStatuses.Closed
            || status == SubmissionStatuses.Completed
            || status == SubmissionStatuses.Cancelled
            || status == SubmissionStatuses.Rejected;
    }
    
    #endregion
    
    #region Transition Recording
    
    public virtual async Task<StatusTransition> RecordTransitionAsync(
        string submissionId,
        string submissionType,
        string fromStatus,
        string toStatus,
        ulong transitionedByUserId,
        string transitionedByUsername,
        string? notes = null,
        string? reasonCode = null)
    {
        try
        {
            // Validate transition
            if (!IsValidTransition(fromStatus, toStatus))
            {
                throw new InvalidOperationException(
                    $"Invalid status transition: {fromStatus} -> {toStatus} for {submissionType} {submissionId}");
            }
            
            // Create transition record
            var transition = new StatusTransition
            {
                SubmissionId = submissionId,
                SubmissionType = submissionType,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                TransitionedAtUtc = DateTime.UtcNow,
                TransitionedByUserId = transitionedByUserId,
                TransitionedByUsername = transitionedByUsername,
                Notes = notes,
                ReasonCode = reasonCode
            };
            
            // Save to storage
            var savedTransition = await SaveTransitionAsync(transition);
            
            LogInfo("Status transition recorded: {SubmissionType} {SubmissionId} from {FromStatus} to {ToStatus} by {UserId}",
                submissionType, submissionId, fromStatus, toStatus, transitionedByUserId);
            
            return savedTransition;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to record status transition for {SubmissionType} {SubmissionId}", 
                submissionType, submissionId);
            throw;
        }
    }
    
    public virtual async Task<List<StatusTransition>> GetTransitionHistoryAsync(string submissionId)
    {
        try
        {
            var transitions = await GetTransitionsFromStorageAsync(submissionId);
            
            // Sort by timestamp descending (most recent first)
            return transitions.OrderByDescending(t => t.TransitionedAtUtc).ToList();
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get transition history for submission: {SubmissionId}", submissionId);
            throw;
        }
    }
    
    public virtual async Task<StatusTransition?> GetLatestTransitionAsync(string submissionId)
    {
        try
        {
            var transitions = await GetTransitionHistoryAsync(submissionId);
            return transitions.FirstOrDefault();
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get latest transition for submission: {SubmissionId}", submissionId);
            throw;
        }
    }
    
    #endregion
    
    #region Transition Rules
    
    // Default: no special permissions required
    // Subclasses override to implement permission checks
    public virtual bool RequiresPermission(string fromStatus, string toStatus)
    {
        return false;
    }
    
    // Default: no permissions required
    // Subclasses override to specify required permissions
    public virtual string? GetRequiredPermission(string fromStatus, string toStatus)
    {
        return null;
    }
    
    #endregion
}