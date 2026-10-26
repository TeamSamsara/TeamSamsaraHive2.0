using System.Globalization;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Database.Firestore;
using TeamSamsara.Hive.Bot.Tickets.Models.DTOs;
using TeamSamsara.Hive.Bot.Tickets.Models.Entities;
using TeamSamsara.Hive.Bot.Tickets.Models.Enums;
using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Tickets.Repositories;

// Firestore-backed ticket storage.
//
// Tickets change constantly and must be correct, so unlike streams/config
// there is no cache: every call reads or writes Firestore directly.
//
// Query strategy: each query sends at most ONE filter to Firestore and does
// any further filtering and all sorting in memory. Combining filters or
// filtering on one field while sorting on another would require building
// composite indexes in the Firebase console; at this bot's ticket volume,
// in-memory work is simpler and costs nothing extra.
//
// Counts use Firestore's count aggregation, which is billed as roughly one
// read per 1,000 matching tickets instead of one read per ticket.
//
// Documents: tickets/{ticket.Id}
// Ticket numbers: counters/tickets { lastNumber } (incremented in a transaction)
public class FirestoreTicketRepository : BaseRepository, ITicketRepository
{
    private const string TicketCounterDocument = "tickets";

    private readonly FirestoreDb _db;
    private readonly CollectionReference _tickets;

    public override string RepositoryName => "FirestoreTicketRepository";

    public FirestoreTicketRepository(FirestoreDb db, ILogger<FirestoreTicketRepository> logger)
        : base(logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _tickets = _db.Collection(FirestoreCollections.Tickets);
    }

    #region Basic CRUD

    public async Task<TicketEntity> CreateAsync(TicketEntity ticket)
    {
        try
        {
            var now = DateTime.UtcNow;
            ticket.CreatedAtUtc = now;
            ticket.UpdatedAtUtc = now;

            await Document(ticket.Id).SetAsync(ToFields(ticket));

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
            var doc = await Document(id).GetSnapshotAsync();
            return doc.Exists ? FromDocument(doc) : null;
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
            var snapshot = await _tickets.WhereEqualTo(Fields.SubmissionId, submissionId).Limit(1).GetSnapshotAsync();
            return snapshot.Documents.Count > 0 ? FromDocument(snapshot.Documents[0]) : null;
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
            ticket.UpdatedAtUtc = DateTime.UtcNow;
            await Document(ticket.Id).SetAsync(ToFields(ticket));

            LogInfo("Ticket updated: {SubmissionId}", ticket.SubmissionId);
            return true;
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

            await Document(id).DeleteAsync();

            LogInfo("Ticket deleted: {SubmissionId}", ticket.SubmissionId);
            return true;
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
            var snapshot = await _tickets.WhereEqualTo(Fields.SubmissionId, submissionId).Limit(1).GetSnapshotAsync();
            return snapshot.Documents.Count > 0;
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
            var tickets = await LoadAsync(BuildServerQuery(status, submittedBy, assignedTo));

            return ApplyFilters(tickets, status, submittedBy, assignedTo)
                .OrderByDescending(t => t.SubmittedAtUtc)
                .ToList();
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
            var tickets = await LoadAsync(BuildServerQuery(filter.Status, filter.SubmittedByUserId, filter.AssignedToUserId));

            IEnumerable<TicketEntity> query = ApplyFilters(tickets, filter.Status, filter.SubmittedByUserId, filter.AssignedToUserId);

            if (filter.Category.HasValue)
            {
                query = query.Where(t => t.Category == filter.Category.Value);
            }

            if (filter.Priority.HasValue)
            {
                query = query.Where(t => t.Priority == filter.Priority.Value);
            }

            if (filter.MinimumPriority.HasValue)
            {
                query = query.Where(t => t.Priority >= filter.MinimumPriority.Value);
            }

            if (filter.Type.HasValue)
            {
                query = query.Where(t => t.Type == filter.Type.Value);
            }

            query = filter.SortBy.ToLowerInvariant() switch
            {
                "priority" => filter.SortDescending
                    ? query.OrderByDescending(t => t.Priority)
                    : query.OrderBy(t => t.Priority),

                "status" => filter.SortDescending
                    ? query.OrderByDescending(t => t.Status, StringComparer.Ordinal)
                    : query.OrderBy(t => t.Status, StringComparer.Ordinal),

                "category" => filter.SortDescending
                    ? query.OrderByDescending(t => t.Category)
                    : query.OrderBy(t => t.Category),

                _ => filter.SortDescending
                    ? query.OrderByDescending(t => t.SubmittedAtUtc)
                    : query.OrderBy(t => t.SubmittedAtUtc)
            };

            return query.Skip(filter.GetSkip()).Take(filter.GetTake()).ToList();
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
            var tickets = await LoadAsync(_tickets.WhereEqualTo(Fields.Status, status));
            return tickets.OrderByDescending(t => t.SubmittedAtUtc).ToList();
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
            var tickets = await LoadAsync(_tickets.WhereEqualTo(Fields.AssignedToUserId, FirestoreValues.FromULong(userId)));
            return ByPriorityThenNewest(tickets);
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
            var tickets = await LoadAsync(_tickets.WhereEqualTo(Fields.SubmittedByUserId, FirestoreValues.FromULong(userId)));
            return tickets.OrderByDescending(t => t.SubmittedAtUtc).ToList();
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
            var tickets = await LoadAsync(_tickets.WhereEqualTo(Fields.Category, FirestoreValues.FromEnum(category)));
            return tickets.OrderByDescending(t => t.SubmittedAtUtc).ToList();
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
            var tickets = await LoadAsync(_tickets.WhereEqualTo(Fields.Priority, FirestoreValues.FromEnum(priority)));
            return tickets.OrderByDescending(t => t.SubmittedAtUtc).ToList();
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
            var tickets = await LoadAsync(
                _tickets.WhereGreaterThanOrEqualTo(Fields.Priority, FirestoreValues.FromEnum(minimumPriority)));
            return ByPriorityThenNewest(tickets);
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
            var tickets = await LoadAsync(
                _tickets.WhereGreaterThanOrEqualTo(Fields.Priority, FirestoreValues.FromEnum(TicketPriority.High)));
            return ByPriorityThenNewest(tickets);
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
            return await CountAsync(_tickets.WhereEqualTo(Fields.Status, status));
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
            return await CountAsync(_tickets.WhereEqualTo(Fields.AssignedToUserId, FirestoreValues.FromULong(userId)));
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
            return await CountAsync(_tickets.WhereEqualTo(Fields.SubmittedByUserId, FirestoreValues.FromULong(userId)));
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
            return await CountAsync(_tickets);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get total ticket count");
            throw;
        }
    }

    #endregion

    #region ID Generation

    // Reserves the next ticket number atomically, so two tickets created at
    // the same moment can never get the same number. A number is used up even
    // if creating that ticket later fails; gaps in numbering are harmless.
    public async Task<string> GenerateNextTicketIdAsync()
    {
        try
        {
            var counterRef = _db.Collection(FirestoreCollections.Counters).Document(TicketCounterDocument);

            // Only needed the very first time (no counter yet): start after the
            // highest existing ticket number, in case tickets were imported.
            long? startFrom = null;
            var counter = await counterRef.GetSnapshotAsync();
            if (!counter.Exists)
            {
                startFrom = await FindHighestTicketNumberAsync();
            }

            var next = await _db.RunTransactionAsync(async transaction =>
            {
                var current = await transaction.GetSnapshotAsync(counterRef);
                var last = current.Exists && current.TryGetValue<long?>("lastNumber", out var value) && value.HasValue
                    ? value.Value
                    : startFrom ?? 0;

                var nextNumber = last + 1;
                transaction.Set(counterRef, new Dictionary<string, object?>
                {
                    ["lastNumber"] = nextNumber,
                    ["updatedAtUtc"] = DateTime.UtcNow
                });
                return nextNumber;
            });

            return $"TICKET-{next:D3}"; // Zero-padded to 3 digits, same as before
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to generate next ticket ID");
            throw;
        }
    }

    private async Task<long> FindHighestTicketNumberAsync()
    {
        var snapshot = await _tickets.GetSnapshotAsync();
        long highest = 0;

        foreach (var doc in snapshot.Documents)
        {
            var parts = FirestoreValues.GetString(doc, Fields.SubmissionId).Split('-');
            if (parts.Length == 2
                && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                && number > highest)
            {
                highest = number;
            }
        }

        return highest;
    }

    #endregion

    #region Helpers

    private DocumentReference Document(Guid id) => _tickets.Document(FirestoreValues.FromGuid(id));

    // Picks the single most selective filter to send to Firestore
    private Query BuildServerQuery(string? status, ulong? submittedBy, ulong? assignedTo)
    {
        if (submittedBy.HasValue)
        {
            return _tickets.WhereEqualTo(Fields.SubmittedByUserId, FirestoreValues.FromULong(submittedBy.Value));
        }

        if (assignedTo.HasValue)
        {
            return _tickets.WhereEqualTo(Fields.AssignedToUserId, FirestoreValues.FromULong(assignedTo.Value));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            return _tickets.WhereEqualTo(Fields.Status, status);
        }

        return _tickets;
    }

    // Re-applies every filter in memory (harmless for the one Firestore already applied)
    private static IEnumerable<TicketEntity> ApplyFilters(
        IEnumerable<TicketEntity> tickets, string? status, ulong? submittedBy, ulong? assignedTo)
    {
        if (!string.IsNullOrWhiteSpace(status))
        {
            tickets = tickets.Where(t => t.Status == status);
        }

        if (submittedBy.HasValue)
        {
            tickets = tickets.Where(t => t.SubmittedByUserId == submittedBy.Value);
        }

        if (assignedTo.HasValue)
        {
            tickets = tickets.Where(t => t.AssignedToUserId == assignedTo.Value);
        }

        return tickets;
    }

    private static List<TicketEntity> ByPriorityThenNewest(IEnumerable<TicketEntity> tickets) =>
        tickets.OrderByDescending(t => t.Priority).ThenByDescending(t => t.SubmittedAtUtc).ToList();

    private static async Task<List<TicketEntity>> LoadAsync(Query query)
    {
        var snapshot = await query.GetSnapshotAsync();
        return snapshot.Documents.Select(FromDocument).ToList();
    }

    private static async Task<int> CountAsync(Query query)
    {
        var snapshot = await query.Count().GetSnapshotAsync();
        return (int)(snapshot.Count ?? 0);
    }

    #endregion

    #region Mapping

    private static class Fields
    {
        public const string SubmissionId = "submissionId";
        public const string Status = "status";
        public const string SubmittedByUserId = "submittedByUserId";
        public const string AssignedToUserId = "assignedToUserId";
        public const string Category = "category";
        public const string Priority = "priority";
    }

    private static Dictionary<string, object?> ToFields(TicketEntity t) => new()
    {
        ["id"] = FirestoreValues.FromGuid(t.Id),
        [Fields.SubmissionId] = t.SubmissionId,
        [Fields.SubmittedByUserId] = FirestoreValues.FromULong(t.SubmittedByUserId),
        ["submittedByUsername"] = t.SubmittedByUsername,
        [Fields.Status] = t.Status,
        ["submittedAtUtc"] = FirestoreValues.FromDateTime(t.SubmittedAtUtc),
        [Fields.AssignedToUserId] = FirestoreValues.FromULong(t.AssignedToUserId),
        ["assignedToUsername"] = t.AssignedToUsername,
        ["assignedAtUtc"] = FirestoreValues.FromDateTime(t.AssignedAtUtc),
        ["resolvedAtUtc"] = FirestoreValues.FromDateTime(t.ResolvedAtUtc),
        ["closedAtUtc"] = FirestoreValues.FromDateTime(t.ClosedAtUtc),
        ["staffNotes"] = t.StaffNotes,
        ["title"] = t.Title,
        ["description"] = t.Description,
        [Fields.Category] = FirestoreValues.FromEnum(t.Category),
        [Fields.Priority] = FirestoreValues.FromEnum(t.Priority),
        ["type"] = FirestoreValues.FromEnum(t.Type),
        ["resolution"] = t.Resolution,
        ["resolvedByUserId"] = FirestoreValues.FromULong(t.ResolvedByUserId),
        ["resolvedByUsername"] = t.ResolvedByUsername,
        ["createdAtUtc"] = FirestoreValues.FromDateTime(t.CreatedAtUtc),
        ["updatedAtUtc"] = FirestoreValues.FromDateTime(t.UpdatedAtUtc),
        ["timeStampUtc"] = FirestoreValues.FromDateTime(t.TimeStampUtc)
    };

    private static TicketEntity FromDocument(DocumentSnapshot doc) => new()
    {
        Id = FirestoreValues.GetGuid(doc, "id", Guid.TryParse(doc.Id, out var docId) ? docId : Guid.NewGuid()),
        SubmissionId = FirestoreValues.GetString(doc, Fields.SubmissionId),
        SubmittedByUserId = FirestoreValues.GetULong(doc, Fields.SubmittedByUserId),
        SubmittedByUsername = FirestoreValues.GetString(doc, "submittedByUsername"),
        Status = FirestoreValues.GetString(doc, Fields.Status),
        SubmittedAtUtc = FirestoreValues.GetDateTime(doc, "submittedAtUtc"),
        AssignedToUserId = FirestoreValues.GetNullableULong(doc, Fields.AssignedToUserId),
        AssignedToUsername = FirestoreValues.GetNullableString(doc, "assignedToUsername"),
        AssignedAtUtc = FirestoreValues.GetNullableDateTime(doc, "assignedAtUtc"),
        ResolvedAtUtc = FirestoreValues.GetNullableDateTime(doc, "resolvedAtUtc"),
        ClosedAtUtc = FirestoreValues.GetNullableDateTime(doc, "closedAtUtc"),
        StaffNotes = FirestoreValues.GetNullableString(doc, "staffNotes"),
        Title = FirestoreValues.GetString(doc, "title"),
        Description = FirestoreValues.GetString(doc, "description"),
        Category = FirestoreValues.GetEnum(doc, Fields.Category, TicketCategory.General),
        Priority = FirestoreValues.GetEnum(doc, Fields.Priority, TicketPriority.Medium),
        Type = FirestoreValues.GetEnum(doc, "type", TicketType.Support),
        Resolution = FirestoreValues.GetNullableString(doc, "resolution"),
        ResolvedByUserId = FirestoreValues.GetNullableULong(doc, "resolvedByUserId"),
        ResolvedByUsername = FirestoreValues.GetNullableString(doc, "resolvedByUsername"),
        CreatedAtUtc = FirestoreValues.GetDateTime(doc, "createdAtUtc"),
        UpdatedAtUtc = FirestoreValues.GetNullableDateTime(doc, "updatedAtUtc"),
        TimeStampUtc = FirestoreValues.GetNullableDateTime(doc, "timeStampUtc") ?? DateTime.UtcNow
    };

    #endregion
}
