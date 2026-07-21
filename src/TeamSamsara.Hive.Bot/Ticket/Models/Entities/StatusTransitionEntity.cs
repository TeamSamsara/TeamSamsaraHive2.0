using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Tickets.Models.Entities;

// Database entity for storing status transition audit trail
// Tracks every status change for tickets
public class StatusTransitionEntity : BaseEntity
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
}