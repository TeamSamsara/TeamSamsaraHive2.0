using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Tickets.Data;
using TeamSamsara.Hive.Bot.Tickets.Models.DTOs;
using TeamSamsara.Hive.Bot.Tickets.Models.Entities;
using TeamSamsara.Hive.Bot.Tickets.Models.Enums;
using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Tickets.Repositories;

// Repository implementation for Ticket operations
// Handles all database access for tickets
public class TicketRepository : BaseRepository, ITicketRepository
{
    private readonly TicketDbContext _context;
    private readonly TicketQueryOptimizer _optimizer;
    
    public override string RepositoryName => "TicketRepository";
    
    #region Constructor
    
    public TicketRepository(
        ILogger<TicketRepository> logger,
        TicketDbContext context,
        TicketQueryOptimizer optimizer)
        : base(logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _optimizer = optimizer ?? throw new ArgumentNullException(nameof(optimizer));
    }
    
    #endregion
    
    #region Basic CRUD
    
    public async Task<TicketEntity> CreateAsync(TicketEntity ticket)
    {
        try
        {
            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync();
            
            LogInfo("Ticket created: {SubmissionId}", ticket.SubmissionId);
            return ticket;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to create ticket: {SubmissionId}", ticket.SubmissionId);
            throw;
        }
    }
    
    public async Task<TicketEntity?> GetByIdAsync(Guid id)
    {
        try
        {
            return await _context.Tickets.FindAsync(id);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get ticket by ID: {Id}", id);
            throw;
        }
    }
    
    public async Task<TicketEntity?> GetBySubmissionIdAsync(string submissionId)
    {
        try
        {
            // Use compiled query from optimizer
            return await _optimizer.GetBySubmissionIdOptimizedAsync(_context, submissionId);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get ticket by SubmissionId: {SubmissionId}", submissionId);
            throw;
        }
    }
    
    public async Task<bool> UpdateAsync(TicketEntity ticket)
    {
        try
        {
            _context.Tickets.Update(ticket);
            var result = await _context.SaveChangesAsync();
            
            LogInfo("Ticket updated: {SubmissionId}", ticket.SubmissionId);
            return result > 0;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to update ticket: {SubmissionId}", ticket.SubmissionId);
            throw;
        }
    }
    
    public async Task<bool> DeleteAsync(Guid id)
    {
        try
        {
            var ticket = await GetByIdAsync(id);
            
            if (ticket == null)
            {
                LogWarning("Cannot delete - ticket not found: {Id}", id);
                return false;
            }
            
            _context.Tickets.Remove(ticket);
            var result = await _context.SaveChangesAsync();
            
            LogInfo("Ticket deleted: {SubmissionId}", ticket.SubmissionId);
            return result > 0;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to delete ticket: {Id}", id);
            throw;
        }
    }
    
    public async Task<bool> ExistsAsync(string submissionId)
    {
        try
        {
            // Use compiled query from optimizer
            return await _optimizer.ExistsBySubmissionIdOptimizedAsync(_context, submissionId);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to check ticket existence: {SubmissionId}", submissionId);
            throw;
        }
    }
    
    #endregion
    
    #region Query Operations
    
    public async Task<List<TicketEntity>> GetAllAsync(
        string? status = null,
        ulong? submittedBy = null,
        ulong? assignedTo = null)
    {
        try
        {
            var query = _optimizer.GetAllOptimized(_context);
            
            // Apply filters
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(t => t.Status == status);
            }
            
            if (submittedBy.HasValue)
            {
                query = query.Where(t => t.SubmittedByUserId == submittedBy.Value);
            }
            
            if (assignedTo.HasValue)
            {
                query = query.Where(t => t.AssignedToUserId == assignedTo.Value);
            }
            
            // Order by submitted date (newest first)
            query = query.OrderByDescending(t => t.SubmittedAtUtc);
            
            return await query.ToListAsync();
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get all tickets");
            throw;
        }
    }
    
    public async Task<List<TicketEntity>> GetFilteredAsync(TicketFilterDto filter)
    {
        try
        {
            var query = _optimizer.GetAllOptimized(_context);
            
            // Apply status filter
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                query = query.Where(t => t.Status == filter.Status);
            }
            
            // Apply category filter
            if (filter.Category.HasValue)
            {
                query = query.Where(t => t.Category == filter.Category.Value);
            }
            
            // Apply priority filter
            if (filter.Priority.HasValue)
            {
                query = query.Where(t => t.Priority == filter.Priority.Value);
            }
            
            // Apply minimum priority filter
            if (filter.MinimumPriority.HasValue)
            {
                query = query.Where(t => t.Priority >= filter.MinimumPriority.Value);
            }
            
            // Apply type filter
            if (filter.Type.HasValue)
            {
                query = query.Where(t => t.Type == filter.Type.Value);
            }
            
            // Apply submitted by filter
            if (filter.SubmittedByUserId.HasValue)
            {
                query = query.Where(t => t.SubmittedByUserId == filter.SubmittedByUserId.Value);
            }
            
            // Apply assigned to filter
            if (filter.AssignedToUserId.HasValue)
            {
                query = query.Where(t => t.AssignedToUserId == filter.AssignedToUserId.Value);
            }
            
            // Apply sorting
            query = filter.SortBy.ToLower() switch
            {
                "priority" => filter.SortDescending
                    ? query.OrderByDescending(t => t.Priority)
                    : query.OrderBy(t => t.Priority),
                
                "status" => filter.SortDescending
                    ? query.OrderByDescending(t => t.Status)
                    : query.OrderBy(t => t.Status),
                
                "category" => filter.SortDescending
                    ? query.OrderByDescending(t => t.Category)
                    : query.OrderBy(t => t.Category),
                
                _ => filter.SortDescending
                    ? query.OrderByDescending(t => t.SubmittedAtUtc)
                    : query.OrderBy(t => t.SubmittedAtUtc)
            };
            
            // Apply pagination
            query = query.Skip(filter.GetSkip()).Take(filter.GetTake());
            
            return await query.ToListAsync();
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get filtered tickets");
            throw;
        }
    }
    
    public async Task<List<TicketEntity>> GetByStatusAsync(string status)
    {
        try
        {
            // Use compiled query from optimizer
            return await _optimizer.GetByStatusOptimizedAsync(_context, status);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get tickets by status: {Status}", status);
            throw;
        }
    }
    
    public async Task<List<TicketEntity>> GetAssignedToAsync(ulong userId)
    {
        try
        {
            // Use compiled query from optimizer
            return await _optimizer.GetAssignedToUserOptimizedAsync(_context, userId);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get tickets assigned to user: {UserId}", userId);
            throw;
        }
    }
    
    public async Task<List<TicketEntity>> GetBySubmitterAsync(ulong userId)
    {
        try
        {
            // Use compiled query from optimizer
            return await _optimizer.GetBySubmitterOptimizedAsync(_context, userId);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get tickets by submitter: {UserId}", userId);
            throw;
        }
    }
    
    public async Task<List<TicketEntity>> GetByCategoryAsync(TicketCategory category)
    {
        try
        {
            var query = _optimizer.GetByCategoryOptimized(_context, category);
            return await query.ToListAsync();
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get tickets by category: {Category}", category);
            throw;
        }
    }
    
    public async Task<List<TicketEntity>> GetByPriorityAsync(TicketPriority priority)
    {
        try
        {
            var query = _optimizer.GetByPriorityOptimized(_context, priority);
            return await query.ToListAsync();
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get tickets by priority: {Priority}", priority);
            throw;
        }
    }
    
    public async Task<List<TicketEntity>> GetByMinimumPriorityAsync(TicketPriority minimumPriority)
    {
        try
        {
            var query = _optimizer.GetByMinimumPriorityOptimized(_context, minimumPriority);
            return await query.ToListAsync();
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get tickets by minimum priority: {MinimumPriority}", minimumPriority);
            throw;
        }
    }
    
    public async Task<List<TicketEntity>> GetHighPriorityAsync()
    {
        try
        {
            // Use compiled query from optimizer
            return await _optimizer.GetHighPriorityOptimizedAsync(_context);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get high priority tickets");
            throw;
        }
    }
    
    #endregion
    
    #region Statistics
    
    public async Task<int> GetCountByStatusAsync(string status)
    {
        try
        {
            return await _context.Tickets
                .Where(t => t.Status == status)
                .CountAsync();
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get count by status: {Status}", status);
            throw;
        }
    }
    
    public async Task<int> GetCountAssignedToAsync(ulong userId)
    {
        try
        {
            return await _context.Tickets
                .Where(t => t.AssignedToUserId == userId)
                .CountAsync();
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get count assigned to user: {UserId}", userId);
            throw;
        }
    }
    
    public async Task<int> GetCountBySubmitterAsync(ulong userId)
    {
        try
        {
            return await _context.Tickets
                .Where(t => t.SubmittedByUserId == userId)
                .CountAsync();
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get count by submitter: {UserId}", userId);
            throw;
        }
    }
    
    public async Task<int> GetTotalCountAsync()
    {
        try
        {
            return await _context.Tickets.CountAsync();
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get total ticket count");
            throw;
        }
    }
    
    #endregion
    
    #region ID Generation
    
    public async Task<string> GenerateNextTicketIdAsync()
    {
        try
        {
            // Get the latest ticket number
            var latestTicket = await _context.Tickets
                .OrderByDescending(t => t.CreatedAtUtc)
                .FirstOrDefaultAsync();
            
            if (latestTicket == null)
            {
                // First ticket
                return "TICKET-001";
            }
            
            // Extract number from SubmissionId (e.g., "TICKET-123" -> 123)
            var parts = latestTicket.SubmissionId.Split('-');
            
            if (parts.Length == 2 && int.TryParse(parts[1], out var lastNumber))
            {
                var nextNumber = lastNumber + 1;
                return $"TICKET-{nextNumber:D3}"; // Zero-padded to 3 digits
            }
            
            // Fallback if parsing fails
            LogWarning("Failed to parse ticket number from: {SubmissionId}, generating new sequence", 
                latestTicket.SubmissionId);
            return "TICKET-001";
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to generate next ticket ID");
            throw;
        }
    }
    
    #endregion
}