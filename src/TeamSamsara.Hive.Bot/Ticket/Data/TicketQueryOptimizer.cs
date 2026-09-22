using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Tickets.Models.Entities;
using TeamSamsara.Hive.Bot.Tickets.Models.Enums;
using TeamSamsara.Hive.Core.Database.Abstractions;

namespace TeamSamsara.Hive.Bot.Tickets.Data;

// Query optimizer for Ticket System
// Provides compiled queries for hot paths and common operations
public class TicketQueryOptimizer : BaseQueryOptimizer
{
    public override string ServiceName => "TicketQueryOptimizer";
    
    #region Constructor
    
    public TicketQueryOptimizer(
        ILogger<TicketQueryOptimizer> logger, 
        IConfiguration configuration) 
        : base(logger, configuration)
    {
    }
    
    #endregion
    
    #region Compiled Queries
    
    // Compiled query: Get ticket by SubmissionId (most frequent operation)
    private static readonly Func<TicketDbContext, string, Task<TicketEntity?>> GetBySubmissionIdCompiled =
        EF.CompileAsyncQuery((TicketDbContext context, string submissionId) =>
            context.Tickets
                .FirstOrDefault(t => t.SubmissionId == submissionId));
    
    // Compiled query: Check if ticket exists by SubmissionId
    private static readonly Func<TicketDbContext, string, Task<bool>> ExistsBySubmissionIdCompiled =
        EF.CompileAsyncQuery((TicketDbContext context, string submissionId) =>
            context.Tickets.Any(t => t.SubmissionId == submissionId));
    
    #endregion
    
    #region Optimized Query Methods
    
    // Get ticket by SubmissionId with compiled query
    public async Task<TicketEntity?> GetBySubmissionIdOptimizedAsync(
        TicketDbContext context, 
        string submissionId)
    {
        return await GetBySubmissionIdCompiled(context, submissionId);
    }
    
    // Check if ticket exists with compiled query
    public async Task<bool> ExistsBySubmissionIdOptimizedAsync(
        TicketDbContext context, 
        string submissionId)
    {
        return await ExistsBySubmissionIdCompiled(context, submissionId);
    }
    
    // Get all tickets with standard optimizations
    public IQueryable<TicketEntity> GetAllOptimized(TicketDbContext context)
    {
        return ApplyStandardOptimizations(context.Tickets.AsQueryable());
    }
    
    // Get tickets by status
    public async Task<List<TicketEntity>> GetByStatusOptimizedAsync(
        TicketDbContext context, 
        string status)
    {
        return await ApplyStandardOptimizations(
                context.Tickets
                    .Where(t => t.Status == status)
                    .OrderByDescending(t => t.SubmittedAtUtc))
            .ToListAsync();
    }
    
    // Get tickets assigned to user
    public async Task<List<TicketEntity>> GetAssignedToUserOptimizedAsync(
        TicketDbContext context, 
        ulong userId)
    {
        return await ApplyStandardOptimizations(
                context.Tickets
                    .Where(t => t.AssignedToUserId == userId)
                    .OrderByDescending(t => t.Priority)
                    .ThenByDescending(t => t.SubmittedAtUtc))
            .ToListAsync();
    }
    
    // Get tickets submitted by user
    public async Task<List<TicketEntity>> GetBySubmitterOptimizedAsync(
        TicketDbContext context, 
        ulong userId)
    {
        return await ApplyStandardOptimizations(
                context.Tickets
                    .Where(t => t.SubmittedByUserId == userId)
                    .OrderByDescending(t => t.SubmittedAtUtc))
            .ToListAsync();
    }
    
    // Get high priority tickets
    public async Task<List<TicketEntity>> GetHighPriorityOptimizedAsync(
        TicketDbContext context)
    {
        return await ApplyStandardOptimizations(
                context.Tickets
                    .Where(t => t.Priority >= TicketPriority.High)
                    .OrderByDescending(t => t.Priority)
                    .ThenByDescending(t => t.SubmittedAtUtc))
            .ToListAsync();
    }
    
    // Get tickets by category with standard optimizations
    public IQueryable<TicketEntity> GetByCategoryOptimized(
        TicketDbContext context, 
        TicketCategory category)
    {
        return ApplyStandardOptimizations(
            context.Tickets
                .Where(t => t.Category == category)
                .OrderByDescending(t => t.SubmittedAtUtc));
    }
    
    // Get tickets by priority with standard optimizations
    public IQueryable<TicketEntity> GetByPriorityOptimized(
        TicketDbContext context, 
        TicketPriority priority)
    {
        return ApplyStandardOptimizations(
            context.Tickets
                .Where(t => t.Priority == priority)
                .OrderByDescending(t => t.SubmittedAtUtc));
    }
    
    // Get tickets by minimum priority with standard optimizations
    public IQueryable<TicketEntity> GetByMinimumPriorityOptimized(
        TicketDbContext context, 
        TicketPriority minimumPriority)
    {
        return ApplyStandardOptimizations(
            context.Tickets
                .Where(t => t.Priority >= minimumPriority)
                .OrderByDescending(t => t.Priority)
                .ThenByDescending(t => t.SubmittedAtUtc));
    }
    
    #endregion
}