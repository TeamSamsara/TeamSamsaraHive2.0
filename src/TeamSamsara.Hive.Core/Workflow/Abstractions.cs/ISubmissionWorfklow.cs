namespace TeamSamsara.Hive.Core.Workflows.Abstractions;

// Interface defining operations for submission-based workflows
// Implement this interface in services that manage submission lifecycles
// Generic type T must inherit from BaseSubmissionEntity
public interface ISubmissionWorkflow<T> where T : BaseSubmissionEntity
{
    #region CRUD Operations
    
    // Create a new submission
    Task<T> CreateSubmissionAsync(T submission);
    
    // Get submission by ID
    Task<T?> GetSubmissionByIdAsync(string submissionId);
    
    // Get all submissions with optional filtering
    Task<List<T>> GetAllSubmissionsAsync(
        string? status = null,
        ulong? submittedBy = null,
        ulong? assignedTo = null);
    
    // Update an existing submission
    Task<bool> UpdateSubmissionAsync(T submission);
    
    // Delete a submission
    Task<bool> DeleteSubmissionAsync(string submissionId);
    
    #endregion
    
    #region Status Transitions
    
    // Transition submission to a new status
    Task<bool> TransitionStatusAsync(
        string submissionId,
        string newStatus,
        ulong staffUserId,
        string? notes = null);
    
    // Check if status transition is valid
    bool IsValidTransition(string currentStatus, string newStatus);
    
    #endregion
    
    #region Assignment Operations
    
    // Assign submission to a staff member
    Task<bool> AssignSubmissionAsync(
        string submissionId,
        ulong assigneeUserId,
        string assigneeUsername);
    
    // Unassign submission from current assignee
    Task<bool> UnassignSubmissionAsync(string submissionId);
    
    #endregion
    
    #region Resolution Operations
    
    // Mark submission as resolved
    Task<bool> ResolveSubmissionAsync(
        string submissionId,
        ulong resolvedByUserId,
        string? resolutionNotes = null);
    
    // Mark submission as closed
    Task<bool> CloseSubmissionAsync(
        string submissionId,
        ulong closedByUserId,
        string? closureNotes = null);
    
    #endregion
    
    #region Query Operations
    
    // Get submissions by status
    Task<List<T>> GetSubmissionsByStatusAsync(string status);
    
    // Get submissions assigned to specific user
    Task<List<T>> GetSubmissionsAssignedToAsync(ulong userId);
    
    // Get submissions submitted by specific user
    Task<List<T>> GetSubmissionsByUserAsync(ulong userId);
    
    // Check if submission exists
    Task<bool> SubmissionExistsAsync(string submissionId);
    
    #endregion
}