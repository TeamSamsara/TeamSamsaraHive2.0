using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Database.Firestore;
using TeamSamsara.Hive.Bot.Protocols.Models;
using TeamSamsara.Hive.Core.Abstractions;
using TeamSamsara.Hive.Core.Interfaces;

namespace TeamSamsara.Hive.Bot.Protocols.Repositories;

// Storage for the server-wide protocol settings
public interface IProtocolSettingsRepository : IRepository
{
    // Never null: returns defaults when nothing has been saved yet
    Task<ProtocolSettings> GetAsync();

    Task SaveAsync(ProtocolSettings settings);
}

// Firestore version: one document, protocolSettings/main, cached in memory.
// Only the bot changes it, so the cache is loaded once and updated on save.
// Registered as a SINGLETON.
public class FirestoreProtocolSettingsRepository : BaseRepository, IProtocolSettingsRepository
{
    private const string DocumentId = "main";

    private readonly DocumentReference _document;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private ProtocolSettings? _cache;

    public override string RepositoryName => "FirestoreProtocolSettingsRepository";

    public FirestoreProtocolSettingsRepository(FirestoreDb db, ILogger<FirestoreProtocolSettingsRepository> logger)
        : base(logger)
    {
        _document = (db ?? throw new ArgumentNullException(nameof(db)))
            .Collection(FirestoreCollections.ProtocolSettings)
            .Document(DocumentId);
    }

    public async Task<ProtocolSettings> GetAsync()
    {
        if (_cache != null)
        {
            return _cache.Copy();
        }

        await _lock.WaitAsync();
        try
        {
            if (_cache == null)
            {
                var snapshot = await _document.GetSnapshotAsync();
                _cache = snapshot.Exists ? FromDocument(snapshot) : new ProtocolSettings();
                LogInfo("Loaded protocol settings ({Owners} owner role(s))", _cache.OwnerRoles.Count);
            }

            return _cache.Copy();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(ProtocolSettings settings)
    {
        await _lock.WaitAsync();
        try
        {
            var toSave = settings.Copy();
            toSave.UpdatedAtUtc = DateTime.UtcNow;

            await _document.SetAsync(ToFields(toSave));
            _cache = toSave;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to save protocol settings");
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    #region Mapping

    private static Dictionary<string, object?> ToFields(ProtocolSettings s) => new()
    {
        ["categoryId"] = FirestoreValues.FromULong(s.CategoryId),
        ["calendarChannelId"] = FirestoreValues.FromULong(s.CalendarChannelId),
        ["digestChannelId"] = FirestoreValues.FromULong(s.DigestChannelId),
        ["plannerRoleId"] = FirestoreValues.FromULong(s.PlannerRoleId),
        ["timeZoneId"] = s.TimeZoneId,
        // A list (not a map) so owner names can contain any characters
        ["ownerRoles"] = s.OwnerRoles
            .Select(o => new Dictionary<string, object?> { ["name"] = o.Name, ["roleId"] = FirestoreValues.FromULong(o.RoleId) })
            .ToList(),
        ["updatedAtUtc"] = FirestoreValues.FromDateTime(s.UpdatedAtUtc),
        ["updatedByUserId"] = FirestoreValues.FromULong(s.UpdatedByUserId),
        ["updatedByUsername"] = s.UpdatedByUsername
    };

    private static ProtocolSettings FromDocument(DocumentSnapshot doc)
    {
        var settings = new ProtocolSettings
        {
            CategoryId = FirestoreValues.GetNullableULong(doc, "categoryId"),
            CalendarChannelId = FirestoreValues.GetNullableULong(doc, "calendarChannelId"),
            DigestChannelId = FirestoreValues.GetNullableULong(doc, "digestChannelId"),
            PlannerRoleId = FirestoreValues.GetNullableULong(doc, "plannerRoleId"),
            TimeZoneId = FirestoreValues.GetString(doc, "timeZoneId", Core.Utilities.ZonedTime.DefaultTimeZoneId),
            UpdatedAtUtc = FirestoreValues.GetNullableDateTime(doc, "updatedAtUtc"),
            UpdatedByUserId = FirestoreValues.GetNullableULong(doc, "updatedByUserId"),
            UpdatedByUsername = FirestoreValues.GetNullableString(doc, "updatedByUsername")
        };

        if (doc.TryGetValue<List<Dictionary<string, object>>>("ownerRoles", out var owners) && owners != null)
        {
            foreach (var owner in owners)
            {
                var name = owner.TryGetValue("name", out var n) ? n as string : null;
                var roleText = owner.TryGetValue("roleId", out var r) ? r as string : null;
                if (!string.IsNullOrWhiteSpace(name) && ulong.TryParse(roleText, out var roleId))
                {
                    settings.OwnerRoles.Add(new OwnerRole { Name = name, RoleId = roleId });
                }
            }
        }

        return settings;
    }

    #endregion
}
