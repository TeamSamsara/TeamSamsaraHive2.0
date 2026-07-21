using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Tickets.Models.Entities;
using TeamSamsara.Hive.Core.Workflows.Constants;
using TeamSamsara.Hive.Core.Workflows.Models;
using TeamSamsara.Hive.Core.Workflows.Services;

namespace TeamSamsara.Hive.Bot.Tickets.Data;

// Status transition service for Ticket System
// Implements ticket-specific state machine rules and audit trail
public class TicketTransitionService : StatusTransitionService
{
    private readonly TicketDbContext _context;

    public override string ServiceName => "TicketTransitionService";
    
    #region Constructor
    
    public TicketTransitionService(
        ILogger<TicketTransitionService> logger, 
        IConfiguration configuration,
        TicketDbContext context) 
        : base(logger, configuration)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }
    
    #endregion
    
    #region Abstract Method Implementation
    
    // Save transition to database
    protected override async Task<StatusTransition> SaveTransitionAsync(StatusTransition transition)
    {
        try
        {
            // Create entity from model
            var entity = new StatusTransitionEntity
            {
                SubmissionId = transition.SubmissionId,
                SubmissionType = transition.SubmissionType,
                FromStatus = transition.FromStatus,
                ToStatus = transition.ToStatus,
                TransitionedAtUtc = transition.TransitionedAtUtc,
                TransitionedByUserId = transition.TransitionedByUserId,
                TransitionedByUsername = transition.TransitionedByUsername,
                Notes = transition.Notes,
                ReasonCode = transition.ReasonCode
            };
            
            _context.StatusTransitions.Add(entity);
            await _context.SaveChangesAsync();
            
            LogInfo("Status transition saved: {SubmissionType} {SubmissionId} from {FromStatus} to {ToStatus}",
                transition.SubmissionType, transition.SubmissionId, transition.FromStatus, transition.ToStatus);
            
            return transition;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to save status transition for {SubmissionType} {SubmissionId}",
                transition.SubmissionType, transition.SubmissionId);
            throw;
        }
    }
    
    // Get transition history from database
    protected override async Task<List<StatusTransition>> GetTransitionsFromStorageAsync(string submissionId)
    {
        try
        {
            var entities = await _context.StatusTransitions
                .Where(t => t.SubmissionId == submissionId)
                .OrderByDescending(t => t.TransitionedAtUtc)
                .ToListAsync();
            
            // Convert entities to models
            var transitions = entities.Select(e => new StatusTransition
            {
                SubmissionId = e.SubmissionId,
                SubmissionType = e.SubmissionType,
                FromStatus = e.FromStatus,
                ToStatus = e.ToStatus,
                TransitionedAtUtc = e.TransitionedAtUtc,
                TransitionedByUserId = e.TransitionedByUserId,
                TransitionedByUsername = e.TransitionedByUsername,
                Notes = e.Notes,
                ReasonCode = e.ReasonCode
            }).ToList();
            
            return transitions;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get transition history for submission: {SubmissionId}", submissionId);
            throw;
        }
    }
    
    #endregion
    
    #region Ticket-Specific State Machine Rules
    
    // Override to implement strict ticket workflow rules
    public override bool IsValidTransition(string fromStatus, string toStatus)
    {
        // First check base validation (prevents same-to-same, terminal states, etc.)
        if (!base.IsValidTransition(fromStatus, toStatus))
        {
            return false;
        }
        
        // Ticket-specific state machine:
        // pending → assigned, in_progress, cancelled
        // assigned → in_progress, resolved, cancelled
        // in_progress → resolved, assigned (reassign), cancelled
        // resolved → closed, in_progress (reopen)
        // Any state → cancelled (user can cancel)
        
        return (fromStatus, toStatus) switch
        {
            // From pending
            (SubmissionStatuses.Pending, SubmissionStatuses.Assigned) => true,
            (SubmissionStatuses.Pending, SubmissionStatuses.InProgress) => true,
            (SubmissionStatuses.Pending, SubmissionStatuses.Cancelled) => true,
            
            // From assigned
            (SubmissionStatuses.Assigned, SubmissionStatuses.InProgress) => true,
            (SubmissionStatuses.Assigned, SubmissionStatuses.Resolved) => true,
            (SubmissionStatuses.Assigned, SubmissionStatuses.Cancelled) => true,
            
            // From in_progress
            (SubmissionStatuses.InProgress, SubmissionStatuses.Resolved) => true,
            (SubmissionStatuses.InProgress, SubmissionStatuses.Assigned) => true, // Reassign
            (SubmissionStatuses.InProgress, SubmissionStatuses.Cancelled) => true,
            
            // From resolved
            (SubmissionStatuses.Resolved, SubmissionStatuses.Closed) => true,
            (SubmissionStatuses.Resolved, SubmissionStatuses.InProgress) => true, // Reopen
            
            // Any state can be cancelled
            (_, SubmissionStatuses.Cancelled) => true,
            
            // All other transitions invalid
            _ => false
        };
    }
    
    // Override to provide valid next statuses for tickets
    public override List<string> GetValidNextStatuses(string currentStatus)
    {
        // Cannot transition from terminal states
        if (IsTerminalStatus(currentStatus))
        {
            return new List<string>();
        }
        
        return currentStatus switch
        {
            SubmissionStatuses.Pending => new List<string>
            {
                SubmissionStatuses.Assigned,
                SubmissionStatuses.InProgress,
                SubmissionStatuses.Cancelled
            },
            
            SubmissionStatuses.Assigned => new List<string>
            {
                SubmissionStatuses.InProgress,
                SubmissionStatuses.Resolved,
                SubmissionStatuses.Cancelled
            },
            
            SubmissionStatuses.InProgress => new List<string>
            {
                SubmissionStatuses.Resolved,
                SubmissionStatuses.Assigned, // Reassign
                SubmissionStatuses.Cancelled
            },
            
            SubmissionStatuses.Resolved => new List<string>
            {
                SubmissionStatuses.Closed,
                SubmissionStatuses.InProgress // Reopen
            },
            
            // Default: only allow cancellation
            _ => new List<string> { SubmissionStatuses.Cancelled }
        };
    }
    
    #endregion
    
    #region Permission Rules
    
    // Override to implement permission requirements for transitions
    public override bool RequiresPermission(string fromStatus, string toStatus)
    {
        // Closing tickets requires admin permission
        if (toStatus == SubmissionStatuses.Closed)
        {
            return true;
        }
        
        // Reopening resolved tickets requires permission
        if (fromStatus == SubmissionStatuses.Resolved && toStatus == SubmissionStatuses.InProgress)
        {
            return true;
        }
        
        // All other transitions don't require special permission
        return false;
    }
    
    // Override to specify required permission level
    public override string? GetRequiredPermission(string fromStatus, string toStatus)
    {
        // Closing tickets requires admin
        if (toStatus == SubmissionStatuses.Closed)
        {
            return "Admin";
        }
        
        // Reopening tickets requires staff
        if (fromStatus == SubmissionStatuses.Resolved && toStatus == SubmissionStatuses.InProgress)
        {
            return "Staff";
        }
        
        return null;
    }
    
    #endregion
}