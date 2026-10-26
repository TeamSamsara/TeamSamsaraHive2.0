using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Database;
using TeamSamsara.Hive.Bot.Database.Firestore;
using TeamSamsara.Hive.Bot.Streams.Models.Entities;
using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Streams.Repositories;

// Firestore-backed tracked-stream storage with an in-memory cache.
//
// StreamMonitorService asks for every tracked stream once a minute. Reading
// Firestore each time would use ~1,440 reads per streamer per day, so:
//   - the full list is loaded from Firestore once (first use, then every
//     Database:Firestore:CacheRefreshHours to pick up console edits)
//   - all reads are served from memory
//   - every change is written to Firestore first, then to the cache
//
// Firestore stays the source of truth, so after a restart the bot still
// knows who was already live and does not re-announce them.
//
// Registered as a SINGLETON so the cache outlives individual DI scopes.
// Only one bot process runs at a time, so nothing else can make it stale
// apart from manual console edits (picked up on the next refresh).
//
// One document per streamer: trackedStreams/{lowercase twitch username}
public class FirestoreStreamRepository : BaseRepository, IStreamRepository
{
    private readonly CollectionReference _collection;
    private readonly TimeSpan _refreshInterval;

    // Guards both reloads and writes, so a reload can never overwrite a
    // change that was written while it was in flight.
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Dictionary<string, TrackedStreamEntity> _cache = new();
    private DateTime _loadedAtUtc = DateTime.MinValue;

    public override string RepositoryName => "FirestoreStreamRepository";

    public FirestoreStreamRepository(
        FirestoreDb db,
        DatabaseSettings settings,
        ILogger<FirestoreStreamRepository> logger) : base(logger)
    {
        _collection = (db ?? throw new ArgumentNullException(nameof(db)))
            .Collection(FirestoreCollections.TrackedStreams);
        _refreshInterval = settings.CacheRefreshInterval;
    }

    #region Reads (served from cache)

    public async Task<TrackedStreamEntity?> GetByUsernameAsync(string twitchUsername)
    {
        await EnsureFreshAsync();
        return _cache.TryGetValue(Normalize(twitchUsername), out var entity) ? Copy(entity) : null;
    }

    public async Task<List<TrackedStreamEntity>> GetAllAsync()
    {
        await EnsureFreshAsync();
        return _cache.Values.Select(Copy).ToList();
    }

    public async Task<bool> ExistsAsync(string twitchUsername)
    {
        await EnsureFreshAsync();
        return _cache.ContainsKey(Normalize(twitchUsername));
    }

    #endregion

    #region Writes (Firestore first, then cache)

    public async Task<TrackedStreamEntity> AddAsync(TrackedStreamEntity entity)
    {
        await EnsureFreshAsync();
        await _lock.WaitAsync();
        try
        {
            entity.TwitchUsername = Normalize(entity.TwitchUsername);
            var now = DateTime.UtcNow;
            entity.CreatedAtUtc = now;
            entity.UpdatedAtUtc = now;

            await _collection.Document(entity.TwitchUsername).SetAsync(ToFields(entity));
            _cache = new Dictionary<string, TrackedStreamEntity>(_cache) { [entity.TwitchUsername] = Copy(entity) };

            LogInfo("Stream added: {Username}", entity.TwitchUsername);
            return entity;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to add stream: {Username}", entity.TwitchUsername);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateAsync(TrackedStreamEntity entity)
    {
        await EnsureFreshAsync();
        await _lock.WaitAsync();
        try
        {
            entity.TwitchUsername = Normalize(entity.TwitchUsername);
            entity.UpdatedAtUtc = DateTime.UtcNow;

            await _collection.Document(entity.TwitchUsername).SetAsync(ToFields(entity));
            _cache = new Dictionary<string, TrackedStreamEntity>(_cache) { [entity.TwitchUsername] = Copy(entity) };
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to update stream: {Username}", entity.TwitchUsername);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> RemoveAsync(string twitchUsername)
    {
        var key = Normalize(twitchUsername);

        await EnsureFreshAsync();
        await _lock.WaitAsync();
        try
        {
            if (!_cache.ContainsKey(key))
            {
                return false;
            }

            await _collection.Document(key).DeleteAsync();

            var updated = new Dictionary<string, TrackedStreamEntity>(_cache);
            updated.Remove(key);
            _cache = updated;

            LogInfo("Stream removed: {Username}", key);
            return true;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to remove stream: {Username}", key);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    #endregion

    #region Cache

    // Loads the list on first use and again once the refresh interval passes
    private async Task EnsureFreshAsync()
    {
        if (DateTime.UtcNow - _loadedAtUtc < _refreshInterval)
        {
            return;
        }

        await _lock.WaitAsync();
        try
        {
            // Another caller may have reloaded while we waited
            if (DateTime.UtcNow - _loadedAtUtc < _refreshInterval)
            {
                return;
            }

            var snapshot = await _collection.GetSnapshotAsync();
            var loaded = new Dictionary<string, TrackedStreamEntity>();
            foreach (var doc in snapshot.Documents)
            {
                var entity = FromDocument(doc);
                loaded[entity.TwitchUsername] = entity;
            }

            _cache = loaded;
            _loadedAtUtc = DateTime.UtcNow;
            LogInfo("Loaded {Count} tracked stream(s) from Firestore", loaded.Count);
        }
        catch (Exception ex)
        {
            // Keep serving the previous list if a refresh fails; on the very
            // first load there is nothing to fall back to, so surface the error.
            if (_loadedAtUtc == DateTime.MinValue)
            {
                LogError(ex, "Failed to load tracked streams from Firestore");
                throw;
            }

            LogWarning("Failed to refresh tracked streams, keeping cached list: {Error}", ex.Message);
            _loadedAtUtc = DateTime.UtcNow; // try again after the next interval, not every minute
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string Normalize(string twitchUsername) => twitchUsername.Trim().ToLowerInvariant();

    #endregion

    #region Mapping

    private static Dictionary<string, object?> ToFields(TrackedStreamEntity e) => new()
    {
        ["id"] = FirestoreValues.FromGuid(e.Id),
        ["twitchUsername"] = e.TwitchUsername,
        ["displayName"] = e.DisplayName,
        ["addedByUserId"] = FirestoreValues.FromULong(e.AddedByUserId),
        ["addedByUsername"] = e.AddedByUsername,
        ["addedAtUtc"] = FirestoreValues.FromDateTime(e.AddedAtUtc),
        ["isLive"] = e.IsLive,
        ["lastLiveAtUtc"] = FirestoreValues.FromDateTime(e.LastLiveAtUtc),
        ["lastOfflineAtUtc"] = FirestoreValues.FromDateTime(e.LastOfflineAtUtc),
        ["lastStreamTitle"] = e.LastStreamTitle,
        ["lastGameName"] = e.LastGameName,
        ["notificationRoleId"] = FirestoreValues.FromULong(e.NotificationRoleId),
        ["createdAtUtc"] = FirestoreValues.FromDateTime(e.CreatedAtUtc),
        ["updatedAtUtc"] = FirestoreValues.FromDateTime(e.UpdatedAtUtc)
    };

    private static TrackedStreamEntity FromDocument(DocumentSnapshot doc) => new()
    {
        Id = FirestoreValues.GetGuid(doc, "id", Guid.NewGuid()),
        TwitchUsername = Normalize(FirestoreValues.GetString(doc, "twitchUsername", doc.Id)),
        DisplayName = FirestoreValues.GetString(doc, "displayName"),
        AddedByUserId = FirestoreValues.GetULong(doc, "addedByUserId"),
        AddedByUsername = FirestoreValues.GetString(doc, "addedByUsername"),
        AddedAtUtc = FirestoreValues.GetDateTime(doc, "addedAtUtc"),
        IsLive = FirestoreValues.GetBool(doc, "isLive"),
        LastLiveAtUtc = FirestoreValues.GetNullableDateTime(doc, "lastLiveAtUtc"),
        LastOfflineAtUtc = FirestoreValues.GetNullableDateTime(doc, "lastOfflineAtUtc"),
        LastStreamTitle = FirestoreValues.GetNullableString(doc, "lastStreamTitle"),
        LastGameName = FirestoreValues.GetNullableString(doc, "lastGameName"),
        NotificationRoleId = FirestoreValues.GetNullableULong(doc, "notificationRoleId"),
        CreatedAtUtc = FirestoreValues.GetDateTime(doc, "createdAtUtc"),
        UpdatedAtUtc = FirestoreValues.GetNullableDateTime(doc, "updatedAtUtc")
    };

    // Callers get their own copy, so changing an entity before UpdateAsync
    // succeeds never leaves the cache out of step with Firestore.
    private static TrackedStreamEntity Copy(TrackedStreamEntity e) => new()
    {
        Id = e.Id,
        TwitchUsername = e.TwitchUsername,
        DisplayName = e.DisplayName,
        AddedByUserId = e.AddedByUserId,
        AddedByUsername = e.AddedByUsername,
        AddedAtUtc = e.AddedAtUtc,
        IsLive = e.IsLive,
        LastLiveAtUtc = e.LastLiveAtUtc,
        LastOfflineAtUtc = e.LastOfflineAtUtc,
        LastStreamTitle = e.LastStreamTitle,
        LastGameName = e.LastGameName,
        NotificationRoleId = e.NotificationRoleId,
        CreatedAtUtc = e.CreatedAtUtc,
        UpdatedAtUtc = e.UpdatedAtUtc,
        TimeStampUtc = e.TimeStampUtc
    };

    #endregion
}
