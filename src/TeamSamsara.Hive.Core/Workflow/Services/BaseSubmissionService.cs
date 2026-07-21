using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Core.Abstractions;
using TeamSamsara.Hive.Core.Workflows.Abstractions;
using TeamSamsara.Hive.Core.Workflows.Models;
using TeamSamsara.Hive.Core.Workflows.Constants;

namespace TeamSamsara.Hive.Core.Workflows.Services;

// Abstract base service implementing common submission workflow operations
// Subclasses must implement repository access and system-specific logic
// Automatically records all status transitions to audit trail
public abstract class BaseSubmissionService<T> : BaseService, ISubmissionWorkflow<T> 
    where T : BaseSubmissionEntity
{
    protected readonly IStatusTransition StatusTransitionService;
    
    #region Constructor
    
    protected BaseSubmissionService(
        ILogger logger, 
        IConfiguration configuration,
        IStatusTransition statusTransitionService) 
        : base(logger, configuration)
    {
        StatusTransitionService = statusTransitionService ?? throw new ArgumentNullException(nameof(statusTransitionService));
    }
    
    #endregion
    
    #region Abstract Methods - Subclasses Must Implement
    
    // Get the submission type name for audit trail (e.g., "ticket", "application")
    protected abstract string GetSubmissionType();
    
    // Get the repository for database operations
    // Subclasses inject their specific repository (TicketRepository, ApplicantRepository)
    protected abstract Task<T?> GetByIdFromRepositoryAsync(string submissionId);
    protected abstract Task<List<T>> GetAllFromRepositoryAsync(string? status, ulong? submittedBy, ulong? assignedTo);
    protected abstract Task<bool> SaveToRepositoryAsync(T submission);
    protected abstract Task<bool> DeleteFromRepositoryAsync(string submissionId);
    protected abstract Task<bool> ExistsInRepositoryAsync(string submissionId);
    
    #endregion
    
    #region CRUD Operations
    
    public virtual async Task<T> CreateSubmissionAsync(T submission)
    {
        try
        {
            // Set initial timestamps
            submission.SubmittedAtUtc = DateTime.UtcNow;
            
            // Save to repository
            var success = await SaveToRepositoryAsync(submission);
            
            if (!success)
            {
                throw new InvalidOperationException($"Failed to create submission: {submission.SubmissionId}");
            }
            
            LogInfo("Submission created: {SubmissionId} by user {UserId}", 
                submission.SubmissionId, submission.SubmittedByUserId);
            
            return submission;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to create submission: {SubmissionId}", submission.SubmissionId);
            throw;
        }
    }
    
    public virtual async Task<T?> GetSubmissionByIdAsync(string submissionId)
    {
        try
        {
            return await GetByIdFromRepositoryAsync(submissionId);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get submission: {SubmissionId}", submissionId);
            throw;
        }
    }
    
    public virtual async Task<List<T>> GetAllSubmissionsAsync(
        string? status = null, 
        ulong? submittedBy = null, 
        ulong? assignedTo = null)
    {
        try
        {
            return await GetAllFromRepositoryAsync(status, submittedBy, assignedTo);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get all submissions");
            throw;
        }
    }
    
    public virtual async Task<bool> UpdateSubmissionAsync(T submission)
    {
        try
        {
            var success = await SaveToRepositoryAsync(submission);
            
            if (success)
            {
                LogInfo("Submission updated: {SubmissionId}", submission.SubmissionId);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to update submission: {SubmissionId}", submission.SubmissionId);
            throw;
        }
    }
    
    public virtual async Task<bool> DeleteSubmissionAsync(string submissionId)
    {
        try
        {
            var success = await DeleteFromRepositoryAsync(submissionId);
            
            if (success)
            {
                LogInfo("Submission deleted: {SubmissionId}", submissionId);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to delete submission: {SubmissionId}", submissionId);
            throw;
        }
    }
    
    #endregion
    
    #region Status Transitions
    
    public virtual async Task<bool> TransitionStatusAsync(
        string submissionId, 
        string newStatus, 
        ulong staffUserId, 
        string? notes = null)
    {
        try
        {
            var submission = await GetByIdFromRepositoryAsync(submissionId);
            
            if (submission == null)
            {
                LogWarning("Cannot transition status - submission not found: {SubmissionId}", submissionId);
                return false;
            }
            
            var oldStatus = submission.Status;
            
            // Validate transition using StatusTransitionService
            if (!StatusTransitionService.IsValidTransition(oldStatus, newStatus))
            {
                LogWarning("Invalid status transition: {OldStatus} -> {NewStatus} for {SubmissionId}", 
                    oldStatus, newStatus, submissionId);
                return false;
            }
            
            // Update status
            submission.Status = newStatus;
            
            // Add notes if provided
            if (!string.IsNullOrWhiteSpace(notes))
            {
                submission.StaffNotes = string.IsNullOrWhiteSpace(submission.StaffNotes) 
                    ? notes 
                    : $"{submission.StaffNotes}\n[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {notes}";
            }
            
            // Update resolution/closure timestamps
            if (newStatus == SubmissionStatuses.Resolved && !submission.ResolvedAtUtc.HasValue)
            {
                submission.ResolvedAtUtc = DateTime.UtcNow;
            }
            
            if ((newStatus == SubmissionStatuses.Closed || newStatus == SubmissionStatuses.Completed) 
                && !submission.ClosedAtUtc.HasValue)
            {
                submission.ClosedAtUtc = DateTime.UtcNow;
            }
            
            // Save entity to repository
            var success = await SaveToRepositoryAsync(submission);
            
            if (success)
            {
                // Record transition to audit trail automatically
                await StatusTransitionService.RecordTransitionAsync(
                    submissionId: submissionId,
                    submissionType: GetSubmissionType(),
                    fromStatus: oldStatus,
                    toStatus: newStatus,
                    transitionedByUserId: staffUserId,
                    transitionedByUsername: submission.AssignedToUsername ?? "System",
                    notes: notes);
                
                LogInfo("Status transition: {SubmissionId} from {OldStatus} to {NewStatus} by staff {StaffId}", 
                    submissionId, oldStatus, newStatus, staffUserId);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to transition status for submission: {SubmissionId}", submissionId);
            throw;
        }
    }
    
    // Default implementation - delegates to StatusTransitionService
    // Subclasses can override for additional custom validation
    public virtual bool IsValidTransition(string currentStatus, string newStatus)
    {
        return StatusTransitionService.IsValidTransition(currentStatus, newStatus);
    }
    
    #endregion
    
    #region Assignment Operations
    
    public virtual async Task<bool> AssignSubmissionAsync(
        string submissionId, 
        ulong assigneeUserId, 
        string assigneeUsername)
    {
        try
        {
            var submission = await GetByIdFromRepositoryAsync(submissionId);
            
            if (submission == null)
            {
                LogWarning("Cannot assign - submission not found: {SubmissionId}", submissionId);
                return false;
            }
            
            var oldStatus = submission.Status;
            
            submission.AssignedToUserId = assigneeUserId;
            submission.AssignedToUsername = assigneeUsername;
            submission.AssignedAtUtc = DateTime.UtcNow;
            
            // Auto-transition to assigned status if currently pending
            if (submission.Status == SubmissionStatuses.Pending)
            {
                submission.Status = SubmissionStatuses.Assigned;
            }
            
            var success = await SaveToRepositoryAsync(submission);
            
            if (success)
            {
                // Record status transition if status changed
                if (oldStatus != submission.Status)
                {
                    await StatusTransitionService.RecordTransitionAsync(
                        submissionId: submissionId,
                        submissionType: GetSubmissionType(),
                        fromStatus: oldStatus,
                        toStatus: submission.Status,
                        transitionedByUserId: assigneeUserId,
                        transitionedByUsername: assigneeUsername,
                        notes: $"Assigned to {assigneeUsername}");
                }
                
                LogInfo("Submission assigned: {SubmissionId} to user {AssigneeId}", 
                    submissionId, assigneeUserId);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to assign submission: {SubmissionId}", submissionId);
            throw;
        }
    }
    
    public virtual async Task<bool> UnassignSubmissionAsync(string submissionId)
    {
        try
        {
            var submission = await GetByIdFromRepositoryAsync(submissionId);
            
            if (submission == null)
            {
                LogWarning("Cannot unassign - submission not found: {SubmissionId}", submissionId);
                return false;
            }
            
            var oldStatus = submission.Status;
            var previousAssignee = submission.AssignedToUsername ?? "Unknown";
            
            submission.AssignedToUserId = null;
            submission.AssignedToUsername = null;
            submission.AssignedAtUtc = null;
            
            // Revert to pending if currently assigned
            if (submission.Status == SubmissionStatuses.Assigned)
            {
                submission.Status = SubmissionStatuses.Pending;
            }
            
            var success = await SaveToRepositoryAsync(submission);
            
            if (success)
            {
                // Record status transition if status changed
                if (oldStatus != submission.Status)
                {
                    await StatusTransitionService.RecordTransitionAsync(
                        submissionId: submissionId,
                        submissionType: GetSubmissionType(),
                        fromStatus: oldStatus,
                        toStatus: submission.Status,
                        transitionedByUserId: 0, // System action
                        transitionedByUsername: "System",
                        notes: $"Unassigned from {previousAssignee}");
                }
                
                LogInfo("Submission unassigned: {SubmissionId}", submissionId);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to unassign submission: {SubmissionId}", submissionId);
            throw;
        }
    }
    
    #endregion
    
    #region Resolution Operations
    
    public virtual async Task<bool> ResolveSubmissionAsync(
        string submissionId, 
        ulong resolvedByUserId, 
        string? resolutionNotes = null)
    {
        return await TransitionStatusAsync(submissionId, SubmissionStatuses.Resolved, resolvedByUserId, resolutionNotes);
    }
    
    public virtual async Task<bool> CloseSubmissionAsync(
        string submissionId, 
        ulong closedByUserId, 
        string? closureNotes = null)
    {
        return await TransitionStatusAsync(submissionId, SubmissionStatuses.Closed, closedByUserId, closureNotes);
    }
    
    #endregion
    
    #region Query Operations
    
    public virtual async Task<List<T>> GetSubmissionsByStatusAsync(string status)
    {
        return await GetAllFromRepositoryAsync(status: status, submittedBy: null, assignedTo: null);
    }
    
    public virtual async Task<List<T>> GetSubmissionsAssignedToAsync(ulong userId)
    {
        return await GetAllFromRepositoryAsync(status: null, submittedBy: null, assignedTo: userId);
    }
    
    public virtual async Task<List<T>> GetSubmissionsByUserAsync(ulong userId)
    {
        return await GetAllFromRepositoryAsync(status: null, submittedBy: userId, assignedTo: null);
    }
    
    public virtual async Task<bool> SubmissionExistsAsync(string submissionId)
    {
        return await ExistsInRepositoryAsync(submissionId);
    }
    
    #endregion
}