using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Tickets.Models.DTOs;
using TeamSamsara.Hive.Bot.Tickets.Models.Entities;
using TeamSamsara.Hive.Bot.Tickets.Models.Enums;
using TeamSamsara.Hive.Bot.Tickets.Repositories;
using TeamSamsara.Hive.Core.Workflows.Abstractions;
using TeamSamsara.Hive.Core.Workflows.Constants;
using TeamSamsara.Hive.Core.Workflows.Services;

namespace TeamSamsara.Hive.Bot.Tickets.Services;

// Ticket service extending base submission workflow
// Handles all ticket business logic and CRUD operations
public class TicketService : BaseSubmissionService<TicketEntity>
{
    private readonly ITicketRepository _repository;
    
    public override string ServiceName => "TicketService";
    
    #region Constructor
    
    public TicketService(
        ILogger<TicketService> logger,
        IConfiguration configuration,
        IStatusTransition statusTransitionService,
        ITicketRepository repository)
        : base(logger, configuration, statusTransitionService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }
    
    #endregion
    
    #region Abstract Method Implementation
    
    // Get submission type name for audit trail
    protected override string GetSubmissionType() => "ticket";
    
    // Get ticket by SubmissionId from repository
    protected override async Task<TicketEntity?> GetByIdFromRepositoryAsync(string submissionId)
    {
        return await _repository.GetBySubmissionIdAsync(submissionId);
    }
    
    // Get all tickets with filtering
    protected override async Task<List<TicketEntity>> GetAllFromRepositoryAsync(
        string? status,
        ulong? submittedBy,
        ulong? assignedTo)
    {
        return await _repository.GetAllAsync(status, submittedBy, assignedTo);
    }
    
    // Save ticket to repository
    protected override async Task<bool> SaveToRepositoryAsync(TicketEntity submission)
    {
        var exists = await _repository.ExistsAsync(submission.SubmissionId);
        
        if (exists)
        {
            return await _repository.UpdateAsync(submission);
        }
        
        await _repository.CreateAsync(submission);
        return true;
    }
    
    // Delete ticket from repository
    protected override async Task<bool> DeleteFromRepositoryAsync(string submissionId)
    {
        var ticket = await _repository.GetBySubmissionIdAsync(submissionId);
        
        if (ticket == null)
        {
            return false;
        }
        
        return await _repository.DeleteAsync(ticket.Id);
    }
    
    // Check if ticket exists
    protected override async Task<bool> ExistsInRepositoryAsync(string submissionId)
    {
        return await _repository.ExistsAsync(submissionId);
    }
    
    #endregion
    
    #region Ticket-Specific Methods
    
    // Create a new ticket from DTO
    public async Task<TicketEntity> CreateTicketAsync(CreateTicketDto dto)
    {
        try
        {
            ValidateCreateTicketDto(dto);
            
            var ticketId = await GenerateTicketIdAsync();
            var ticket = BuildTicketEntityFromDto(dto, ticketId);
            var created = await CreateSubmissionAsync(ticket);
            
            LogInfo("Ticket created: {TicketId} by user {UserId}", ticketId, dto.SubmittedByUserId);
            
            return created;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to create ticket from DTO");
            throw;
        }
    }
    
    // Update ticket from DTO
    public async Task<bool> UpdateTicketAsync(UpdateTicketDto dto)
    {
        try
        {
            ValidateUpdateTicketDto(dto);
            
            var ticket = await GetTicketOrThrowAsync(dto.TicketId);
            
            // Handle status change separately (uses transition logic with audit trail)
            if (IsStatusChanging(dto, ticket))
            {
                return await TransitionStatusAsync(
                    dto.TicketId,
                    dto.Status!,
                    dto.UpdatedByUserId,
                    dto.StaffNotes);
            }
            
            ApplyTicketUpdates(dto, ticket);
            
            return await UpdateSubmissionAsync(ticket);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to update ticket from DTO");
            throw;
        }
    }
    
    // Get tickets by category
    public async Task<List<TicketEntity>> GetByCategoryAsync(TicketCategory category)
    {
        try
        {
            return await _repository.GetByCategoryAsync(category);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get tickets by category: {Category}", category);
            throw;
        }
    }
    
    // Get tickets by priority
    public async Task<List<TicketEntity>> GetByPriorityAsync(TicketPriority priority)
    {
        try
        {
            return await _repository.GetByPriorityAsync(priority);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get tickets by priority: {Priority}", priority);
            throw;
        }
    }
    
    // Get high priority tickets (High, Critical, Emergency)
    public async Task<List<TicketEntity>> GetHighPriorityTicketsAsync()
    {
        try
        {
            return await _repository.GetHighPriorityAsync();
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get high priority tickets");
            throw;
        }
    }
    
    // Get tickets with advanced filtering
    public async Task<List<TicketEntity>> GetFilteredTicketsAsync(TicketFilterDto filter)
    {
        ValidateFilterPagination(filter);
        
        try
        {
            return await _repository.GetFilteredAsync(filter);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get filtered tickets");
            throw;
        }
    }
    
    // Resolve ticket with resolution notes
    public async Task<bool> ResolveTicketAsync(
        string ticketId,
        ulong resolvedByUserId,
        string resolvedByUsername,
        string resolution)
    {
        try
        {
            var ticket = await GetTicketOrThrowAsync(ticketId);
            
            ApplyResolutionToTicket(ticket, resolvedByUserId, resolvedByUsername, resolution);
            
            return await ResolveSubmissionAsync(
                ticketId, 
                resolvedByUserId, 
                BuildResolutionNotes(resolvedByUsername, resolution));
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to resolve ticket: {TicketId}", ticketId);
            throw;
        }
    }
    
    // Get ticket statistics
    public async Task<TicketStatistics> GetStatisticsAsync()
    {
        try
        {
            var total = await _repository.GetTotalCountAsync();
            var statusCounts = await GetAllStatusCountsAsync();
            
            return BuildStatistics(total, statusCounts);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get ticket statistics");
            throw;
        }
    }
    
    #endregion
    
    #region Private Helper Methods
    
    // Validate CreateTicketDto and throw if invalid
    private void ValidateCreateTicketDto(CreateTicketDto dto)
    {
        if (!dto.IsValid(out var errorMessage))
        {
            throw new ArgumentException(errorMessage);
        }
    }
    
    // Validate UpdateTicketDto and throw if invalid
    private void ValidateUpdateTicketDto(UpdateTicketDto dto)
    {
        if (!dto.IsValid(out var errorMessage))
        {
            throw new ArgumentException(errorMessage);
        }
    }
    
    // Validate filter pagination and throw if invalid
    private void ValidateFilterPagination(TicketFilterDto filter)
    {
        if (!filter.IsValid(out var errorMessage))
        {
            throw new ArgumentException(errorMessage);
        }
    }
    
    // Generate next ticket ID
    private async Task<string> GenerateTicketIdAsync()
    {
        try
        {
            return await _repository.GenerateNextTicketIdAsync();
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to generate ticket ID");
            throw;
        }
    }
    
    // Build ticket entity from DTO
    private TicketEntity BuildTicketEntityFromDto(CreateTicketDto dto, string ticketId)
    {
        return new TicketEntity
        {
            SubmissionId = ticketId,
            SubmittedByUserId = dto.SubmittedByUserId,
            SubmittedByUsername = dto.SubmittedByUsername,
            SubmittedAtUtc = DateTime.UtcNow,
            Status = SubmissionStatuses.Pending,
            Title = dto.Title,
            Description = dto.Description,
            Category = dto.Category,
            Priority = dto.Priority,
            Type = dto.Type
        };
    }
    
    // Get ticket or throw if not found
    private async Task<TicketEntity> GetTicketOrThrowAsync(string ticketId)
    {
        var ticket = await GetByIdFromRepositoryAsync(ticketId);
        
        if (ticket == null)
        {
            throw new InvalidOperationException($"Ticket not found: {ticketId}");
        }
        
        return ticket;
    }
    
    // Check if status is changing
    private bool IsStatusChanging(UpdateTicketDto dto, TicketEntity ticket)
    {
        return dto.Status != null && dto.Status != ticket.Status;
    }
    
    // Apply all updates from DTO to ticket entity
    private void ApplyTicketUpdates(UpdateTicketDto dto, TicketEntity ticket)
    {
        if (dto.Title != null)
            ticket.Title = dto.Title;
        
        if (dto.Description != null)
            ticket.Description = dto.Description;
        
        if (dto.Category.HasValue)
            ticket.Category = dto.Category.Value;
        
        if (dto.Priority.HasValue)
            ticket.Priority = dto.Priority.Value;
        
        if (dto.Type.HasValue)
            ticket.Type = dto.Type.Value;
        
        if (dto.Resolution != null)
        {
            ticket.Resolution = dto.Resolution;
            ticket.ResolvedByUserId = dto.UpdatedByUserId;
            ticket.ResolvedByUsername = dto.UpdatedByUsername;
        }
    }
    
    // Apply resolution details to ticket
    private void ApplyResolutionToTicket(
        TicketEntity ticket,
        ulong resolvedByUserId,
        string resolvedByUsername,
        string resolution)
    {
        ticket.Resolution = resolution;
        ticket.ResolvedByUserId = resolvedByUserId;
        ticket.ResolvedByUsername = resolvedByUsername;
    }
    
    // Build resolution notes string
    private string BuildResolutionNotes(string resolvedByUsername, string resolution)
    {
        return $"Resolved by {resolvedByUsername}: {resolution}";
    }
    
    // Get all status counts in parallel
    private async Task<Dictionary<string, int>> GetAllStatusCountsAsync()
    {
        var tasks = new[]
        {
            _repository.GetCountByStatusAsync(SubmissionStatuses.Pending),
            _repository.GetCountByStatusAsync(SubmissionStatuses.Assigned),
            _repository.GetCountByStatusAsync(SubmissionStatuses.InProgress),
            _repository.GetCountByStatusAsync(SubmissionStatuses.Resolved),
            _repository.GetCountByStatusAsync(SubmissionStatuses.Closed)
        };
        
        var results = await Task.WhenAll(tasks);
        
        return new Dictionary<string, int>
        {
            [SubmissionStatuses.Pending] = results[0],
            [SubmissionStatuses.Assigned] = results[1],
            [SubmissionStatuses.InProgress] = results[2],
            [SubmissionStatuses.Resolved] = results[3],
            [SubmissionStatuses.Closed] = results[4]
        };
    }
    
    // Build statistics object
    private TicketStatistics BuildStatistics(int total, Dictionary<string, int> statusCounts)
    {
        return new TicketStatistics
        {
            TotalTickets = total,
            PendingTickets = statusCounts[SubmissionStatuses.Pending],
            AssignedTickets = statusCounts[SubmissionStatuses.Assigned],
            InProgressTickets = statusCounts[SubmissionStatuses.InProgress],
            ResolvedTickets = statusCounts[SubmissionStatuses.Resolved],
            ClosedTickets = statusCounts[SubmissionStatuses.Closed]
        };
    }
    
    #endregion
}

// Helper class for ticket statistics
public class TicketStatistics
{
    public int TotalTickets { get; set; }
    public int PendingTickets { get; set; }
    public int AssignedTickets { get; set; }
    public int InProgressTickets { get; set; }
    public int ResolvedTickets { get; set; }
    public int ClosedTickets { get; set; }
}