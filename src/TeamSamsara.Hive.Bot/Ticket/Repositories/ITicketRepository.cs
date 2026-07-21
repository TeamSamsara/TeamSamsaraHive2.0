using TeamSamsara.Hive.Bot.Tickets.Models.DTOs;
using TeamSamsara.Hive.Bot.Tickets.Models.Entities;
using TeamSamsara.Hive.Bot.Tickets.Models.Enums;
using TeamSamsara.Hive.Core.Abstractions;
using TeamSamsara.Hive.Core.Interfaces;

namespace TeamSamsara.Hive.Bot.Tickets.Repositories;

// Repository interface for Ticket operations
// Defines contract for ticket data access
public interface ITicketRepository : IRepository
{
    #region Basic CRUD
    
    // Create a new ticket
    Task<TicketEntity> CreateAsync(TicketEntity ticket);
    
    // Get ticket by ID
    Task<TicketEntity?> GetByIdAsync(Guid id);
    
    // Get ticket by SubmissionId (e.g., "TICKET-001")
    Task<TicketEntity?> GetBySubmissionIdAsync(string submissionId);
    
    // Update an existing ticket
    Task<bool> UpdateAsync(TicketEntity ticket);
    
    // Delete a ticket
    Task<bool> DeleteAsync(Guid id);
    
    // Check if ticket exists
    Task<bool> ExistsAsync(string submissionId);
    
    #endregion
    
    #region Query Operations
    
    // Get all tickets with optional filtering
    Task<List<TicketEntity>> GetAllAsync(
        string? status = null,
        ulong? submittedBy = null,
        ulong? assignedTo = null);
    
    // Get tickets with advanced filtering
    Task<List<TicketEntity>> GetFilteredAsync(TicketFilterDto filter);
    
    // Get tickets by status
    Task<List<TicketEntity>> GetByStatusAsync(string status);
    
    // Get tickets assigned to a user
    Task<List<TicketEntity>> GetAssignedToAsync(ulong userId);
    
    // Get tickets submitted by a user
    Task<List<TicketEntity>> GetBySubmitterAsync(ulong userId);
    
    // Get tickets by category
    Task<List<TicketEntity>> GetByCategoryAsync(TicketCategory category);
    
    // Get tickets by priority
    Task<List<TicketEntity>> GetByPriorityAsync(TicketPriority priority);
    
    // Get tickets by minimum priority (High, Critical, Emergency)
    Task<List<TicketEntity>> GetByMinimumPriorityAsync(TicketPriority minimumPriority);
    
    // Get high priority tickets (High, Critical, Emergency)
    Task<List<TicketEntity>> GetHighPriorityAsync();
    
    #endregion
    
    #region Statistics
    
    // Get count of tickets by status
    Task<int> GetCountByStatusAsync(string status);
    
    // Get count of tickets assigned to user
    Task<int> GetCountAssignedToAsync(ulong userId);
    
    // Get count of tickets submitted by user
    Task<int> GetCountBySubmitterAsync(ulong userId);
    
    // Get total ticket count
    Task<int> GetTotalCountAsync();
    
    #endregion
    
    #region ID Generation
    
    // Generate next ticket ID (e.g., "TICKET-001")
    Task<string> GenerateNextTicketIdAsync();
    
    #endregion
}