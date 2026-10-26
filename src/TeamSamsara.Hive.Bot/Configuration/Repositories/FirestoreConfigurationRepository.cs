using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Configuration.Models.Entities;
using TeamSamsara.Hive.Bot.Database;
using TeamSamsara.Hive.Bot.Database.Firestore;
using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Configuration.Repositories;

// Firestore-backed configuration storage with an in-memory cache.
//
// Config values (channel and role IDs) are read constantly and change rarely,
// so the whole set (~16 documents) is loaded once and served from memory.
// Changes made through /config are written to Firestore and the cache at the
// same time; edits made in the Firebase console are picked up on the next
// refresh (Database:Firestore:CacheRefreshHours).
//
// Registered as a SINGLETON so the cache outlives individual DI scopes.
//
// One document per key: configuration/{key}
public class FirestoreConfigurationRepository : BaseRepository, IConfigurationRepository
{
    private readonly CollectionReference _collection;
    private readonly TimeSpan _refreshInterval;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private Dictionary<string, ConfigurationEntity> _cache = new(StringComparer.Ordinal);
    private DateTime _loadedAtUtc = DateTime.MinValue;

    public override string RepositoryName => "FirestoreConfigurationRepository";

    public FirestoreConfigurationRepository(
        FirestoreDb db,
        DatabaseSettings settings,
        ILogger<FirestoreConfigurationRepository> logger) : base(logger)
    {
        _collection = (db ?? throw new ArgumentNullException(nameof(db)))
            .Collection(FirestoreCollections.Configuration);
        _refreshInterval = settings.CacheRefreshInterval;
    }

    #region Reads (served from cache)

    public async Task<ConfigurationEntity?> GetByKeyAsync(string key)
    {
        await EnsureFreshAsync();
        return _cache.TryGetValue(key, out var entity) ? Copy(entity) : null;
    }

    public async Task<List<ConfigurationEntity>> GetAllAsync()
    {
        await EnsureFreshAsync();
        return _cache.Values
            .OrderBy(c => c.Key, StringComparer.Ordinal)
            .Select(Copy)
            .ToList();
    }

    public async Task<bool> ExistsAsync(string key)
    {
        await EnsureFreshAsync();
        return _cache.ContainsKey(key);
    }

    #endregion

    #region Writes (Firestore first, then cache)

    public async Task UpsertAsync(ConfigurationEntity entity)
    {
        await EnsureFreshAsync();
        await _lock.WaitAsync();
        try
        {
            var now = DateTime.UtcNow;
            ConfigurationEntity toSave;

            if (_cache.TryGetValue(entity.Key, out var existing))
            {
                // Same behaviour as the SQLite repository: keep identity and
                // creation time, replace the value and audit fields.
                toSave = Copy(existing);
                toSave.Value = entity.Value;
                toSave.DisplayName = entity.DisplayName;
                toSave.LastUpdatedUtc = entity.LastUpdatedUtc;
                toSave.LastUpdatedByUserId = entity.LastUpdatedByUserId;
                toSave.LastUpdatedByUsername = entity.LastUpdatedByUsername;
            }
            else
            {
                toSave = Copy(entity);
                toSave.CreatedAtUtc = now;
            }

            toSave.UpdatedAtUtc = now;

            await _collection.Document(toSave.Key).SetAsync(ToFields(toSave));
            _cache = new Dictionary<string, ConfigurationEntity>(_cache, StringComparer.Ordinal) { [toSave.Key] = toSave };

            LogInfo("Configuration {Key} upserted successfully", entity.Key);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to upsert configuration: {Key}", entity.Key);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(string key)
    {
        await EnsureFreshAsync();
        await _lock.WaitAsync();
        try
        {
            if (!_cache.ContainsKey(key))
            {
                LogWarning("Configuration {Key} not found for deletion", key);
                return;
            }

            await _collection.Document(key).DeleteAsync();

            var updated = new Dictionary<string, ConfigurationEntity>(_cache, StringComparer.Ordinal);
            updated.Remove(key);
            _cache = updated;

            LogInfo("Configuration {Key} deleted successfully", key);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to delete configuration: {Key}", key);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    #endregion

    #region Cache

    private async Task EnsureFreshAsync()
    {
        if (DateTime.UtcNow - _loadedAtUtc < _refreshInterval)
        {
            return;
        }

        await _lock.WaitAsync();
        try
        {
            if (DateTime.UtcNow - _loadedAtUtc < _refreshInterval)
            {
                return;
            }

            var snapshot = await _collection.GetSnapshotAsync();
            var loaded = new Dictionary<string, ConfigurationEntity>(StringComparer.Ordinal);
            foreach (var doc in snapshot.Documents)
            {
                var entity = FromDocument(doc);
                loaded[entity.Key] = entity;
            }

            _cache = loaded;
            _loadedAtUtc = DateTime.UtcNow;
            LogInfo("Loaded {Count} configuration value(s) from Firestore", loaded.Count);
        }
        catch (Exception ex)
        {
            if (_loadedAtUtc == DateTime.MinValue)
            {
                LogError(ex, "Failed to load configuration from Firestore");
                throw;
            }

            LogWarning("Failed to refresh configuration, keeping cached values: {Error}", ex.Message);
            _loadedAtUtc = DateTime.UtcNow;
        }
        finally
        {
            _lock.Release();
        }
    }

    #endregion

    #region Mapping

    private static Dictionary<string, object?> ToFields(ConfigurationEntity e) => new()
    {
        ["id"] = FirestoreValues.FromGuid(e.Id),
        ["key"] = e.Key,
        ["value"] = e.Value,
        ["displayName"] = e.DisplayName,
        ["lastUpdatedUtc"] = FirestoreValues.FromDateTime(e.LastUpdatedUtc),
        ["lastUpdatedByUserId"] = FirestoreValues.FromULong(e.LastUpdatedByUserId),
        ["lastUpdatedByUsername"] = e.LastUpdatedByUsername,
        ["createdAtUtc"] = FirestoreValues.FromDateTime(e.CreatedAtUtc),
        ["updatedAtUtc"] = FirestoreValues.FromDateTime(e.UpdatedAtUtc)
    };

    private static ConfigurationEntity FromDocument(DocumentSnapshot doc) => new()
    {
        Id = FirestoreValues.GetGuid(doc, "id", Guid.NewGuid()),
        Key = FirestoreValues.GetString(doc, "key", doc.Id),
        Value = FirestoreValues.GetString(doc, "value"),
        DisplayName = FirestoreValues.GetNullableString(doc, "displayName"),
        LastUpdatedUtc = FirestoreValues.GetDateTime(doc, "lastUpdatedUtc"),
        LastUpdatedByUserId = FirestoreValues.GetULong(doc, "lastUpdatedByUserId"),
        LastUpdatedByUsername = FirestoreValues.GetString(doc, "lastUpdatedByUsername"),
        CreatedAtUtc = FirestoreValues.GetDateTime(doc, "createdAtUtc"),
        UpdatedAtUtc = FirestoreValues.GetNullableDateTime(doc, "updatedAtUtc")
    };

    private static ConfigurationEntity Copy(ConfigurationEntity e) => new()
    {
        Id = e.Id,
        Key = e.Key,
        Value = e.Value,
        DisplayName = e.DisplayName,
        LastUpdatedUtc = e.LastUpdatedUtc,
        LastUpdatedByUserId = e.LastUpdatedByUserId,
        LastUpdatedByUsername = e.LastUpdatedByUsername,
        CreatedAtUtc = e.CreatedAtUtc,
        UpdatedAtUtc = e.UpdatedAtUtc,
        TimeStampUtc = e.TimeStampUtc
    };

    #endregion
}
