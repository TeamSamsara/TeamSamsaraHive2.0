using Google.Cloud.Firestore;
using Microsoft.EntityFrameworkCore;
using TeamSamsara.Hive.Bot.Database.Firestore;
using TeamSamsara.Hive.Bot.Tickets.Models.Entities;
using TeamSamsara.Hive.Core.Workflows.Models;

namespace TeamSamsara.Hive.Bot.Tickets.Data;

// Where TicketTransitionService keeps the ticket status history (audit trail).
// The state-machine rules stay in TicketTransitionService; only storage
// differs between backends.
public interface IStatusTransitionStore
{
    Task SaveAsync(StatusTransition transition);

    // Newest first
    Task<List<StatusTransition>> GetForSubmissionAsync(string submissionId);
}

#region SQLite

// Original storage: the StatusTransitions table in tickets.db
public class SqliteStatusTransitionStore : IStatusTransitionStore
{
    private readonly TicketDbContext _context;

    public SqliteStatusTransitionStore(TicketDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task SaveAsync(StatusTransition transition)
    {
        _context.StatusTransitions.Add(new StatusTransitionEntity
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
        });

        await _context.SaveChangesAsync();
    }

    public async Task<List<StatusTransition>> GetForSubmissionAsync(string submissionId)
    {
        var entities = await _context.StatusTransitions
            .Where(t => t.SubmissionId == submissionId)
            .OrderByDescending(t => t.TransitionedAtUtc)
            .ToListAsync();

        return entities.Select(e => new StatusTransition
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
    }
}

#endregion

#region Firestore

// Firestore storage: statusTransitions/{generated id}, one document per change.
// History is fetched by submissionId and sorted in memory (sorting on a
// different field than the filter would need a composite index).
public class FirestoreStatusTransitionStore : IStatusTransitionStore
{
    private readonly CollectionReference _collection;

    public FirestoreStatusTransitionStore(FirestoreDb db)
    {
        _collection = (db ?? throw new ArgumentNullException(nameof(db)))
            .Collection(FirestoreCollections.StatusTransitions);
    }

    public async Task SaveAsync(StatusTransition transition)
    {
        var id = Guid.NewGuid();
        await _collection.Document(FirestoreValues.FromGuid(id)).SetAsync(new Dictionary<string, object?>
        {
            ["id"] = FirestoreValues.FromGuid(id),
            ["submissionId"] = transition.SubmissionId,
            ["submissionType"] = transition.SubmissionType,
            ["fromStatus"] = transition.FromStatus,
            ["toStatus"] = transition.ToStatus,
            ["transitionedAtUtc"] = FirestoreValues.FromDateTime(transition.TransitionedAtUtc),
            ["transitionedByUserId"] = FirestoreValues.FromULong(transition.TransitionedByUserId),
            ["transitionedByUsername"] = transition.TransitionedByUsername,
            ["notes"] = transition.Notes,
            ["reasonCode"] = transition.ReasonCode,
            ["createdAtUtc"] = DateTime.UtcNow
        });
    }

    public async Task<List<StatusTransition>> GetForSubmissionAsync(string submissionId)
    {
        var snapshot = await _collection.WhereEqualTo("submissionId", submissionId).GetSnapshotAsync();

        return snapshot.Documents
            .Select(doc => new StatusTransition
            {
                SubmissionId = FirestoreValues.GetString(doc, "submissionId"),
                SubmissionType = FirestoreValues.GetString(doc, "submissionType"),
                FromStatus = FirestoreValues.GetString(doc, "fromStatus"),
                ToStatus = FirestoreValues.GetString(doc, "toStatus"),
                TransitionedAtUtc = FirestoreValues.GetDateTime(doc, "transitionedAtUtc"),
                TransitionedByUserId = FirestoreValues.GetULong(doc, "transitionedByUserId"),
                TransitionedByUsername = FirestoreValues.GetString(doc, "transitionedByUsername"),
                Notes = FirestoreValues.GetNullableString(doc, "notes"),
                ReasonCode = FirestoreValues.GetNullableString(doc, "reasonCode")
            })
            .OrderByDescending(t => t.TransitionedAtUtc)
            .ToList();
    }
}

#endregion
