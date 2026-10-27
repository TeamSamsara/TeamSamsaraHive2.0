using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Database;
using TeamSamsara.Hive.Bot.Database.Firestore;
using TeamSamsara.Hive.Bot.Protocols.Models;
using TeamSamsara.Hive.Core.Abstractions;
using TeamSamsara.Hive.Core.Interfaces;

namespace TeamSamsara.Hive.Bot.Protocols.Repositories;

public interface ITemplateRepository : IRepository
{
    Task<List<ProtocolTemplate>> GetAllAsync();

    Task<ProtocolTemplate?> GetAsync(string id);

    // Creates or replaces the template with this id
    Task SaveAsync(ProtocolTemplate template);

    Task<bool> DeleteAsync(string id);
}

// Firestore version: one document per template, protocolTemplates/{id}, with
// the steps nested inside it (a template is always read and written whole).
//
// Templates are read for every autocomplete keystroke, so they are cached in
// memory: loaded on first use, re-read every CacheRefreshHours to pick up
// console edits, and updated immediately on every save.
// Registered as a SINGLETON.
public class FirestoreTemplateRepository : BaseRepository, ITemplateRepository
{
    private readonly CollectionReference _collection;
    private readonly TimeSpan _refreshInterval;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Dictionary<string, ProtocolTemplate> _cache = new(StringComparer.Ordinal);
    private DateTime _loadedAtUtc = DateTime.MinValue;

    public override string RepositoryName => "FirestoreTemplateRepository";

    public FirestoreTemplateRepository(FirestoreDb db, DatabaseSettings settings, ILogger<FirestoreTemplateRepository> logger)
        : base(logger)
    {
        _collection = (db ?? throw new ArgumentNullException(nameof(db))).Collection(FirestoreCollections.ProtocolTemplates);
        _refreshInterval = settings.CacheRefreshInterval;
    }

    #region Reads (served from cache)

    public async Task<List<ProtocolTemplate>> GetAllAsync()
    {
        await EnsureFreshAsync();
        return _cache.Values
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select(t => t.Copy())
            .ToList();
    }

    public async Task<ProtocolTemplate?> GetAsync(string id)
    {
        await EnsureFreshAsync();
        return _cache.TryGetValue(id, out var template) ? template.Copy() : null;
    }

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
            var loaded = new Dictionary<string, ProtocolTemplate>(StringComparer.Ordinal);
            foreach (var doc in snapshot.Documents)
            {
                try
                {
                    loaded[doc.Id] = FromDocument(doc);
                }
                catch (Exception ex)
                {
                    LogError(ex, "Skipping unreadable template document {Id}", doc.Id);
                }
            }

            _cache = loaded;
            _loadedAtUtc = DateTime.UtcNow;
            LogInfo("Loaded {Count} protocol template(s) from Firestore", loaded.Count);
        }
        finally
        {
            _lock.Release();
        }
    }

    #endregion

    #region Writes (Firestore first, then cache)

    public async Task SaveAsync(ProtocolTemplate template)
    {
        await EnsureFreshAsync();
        await _lock.WaitAsync();
        try
        {
            var toSave = template.Copy();
            var now = DateTime.UtcNow;
            toSave.CreatedAtUtc ??= _cache.TryGetValue(toSave.Id, out var existing) ? existing.CreatedAtUtc ?? now : now;
            toSave.UpdatedAtUtc = now;

            await _collection.Document(toSave.Id).SetAsync(ToFields(toSave));
            _cache = new Dictionary<string, ProtocolTemplate>(_cache, StringComparer.Ordinal) { [toSave.Id] = toSave };
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to save template {Id}", template.Id);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await EnsureFreshAsync();
        await _lock.WaitAsync();
        try
        {
            if (!_cache.ContainsKey(id))
            {
                return false;
            }

            await _collection.Document(id).DeleteAsync();
            var updated = new Dictionary<string, ProtocolTemplate>(_cache, StringComparer.Ordinal);
            updated.Remove(id);
            _cache = updated;
            return true;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to delete template {Id}", id);
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    #endregion

    #region Mapping

    private static Dictionary<string, object?> ToFields(ProtocolTemplate t) => new()
    {
        ["name"] = t.Name,
        ["description"] = t.Description,
        ["opens"] = t.Opens,
        ["fields"] = t.Fields.Select(f => new Dictionary<string, object?>
        {
            ["key"] = f.Key,
            ["label"] = f.Label,
            ["required"] = f.Required
        }).ToList(),
        ["steps"] = t.Steps.Select(s => new Dictionary<string, object?>
        {
            ["id"] = s.Id,
            ["title"] = s.Title,
            ["phase"] = s.Phase,
            ["owner"] = s.Owner,
            ["due"] = s.Due,
            ["where"] = s.Where,
            ["instructions"] = s.Instructions,
            ["assetsUrl"] = s.AssetsUrl,
            ["exampleUrl"] = s.ExampleUrl,
            ["posts"] = s.Posts.ToDictionary(
                p => p.Key,
                p => (object?)new Dictionary<string, object?>
                {
                    ["fields"] = p.Value.Fields.ToDictionary(f => f.Key, f => (object?)f.Value),
                    ["checklist"] = p.Value.Checklist.ToList()
                })
        }).ToList(),
        ["createdAtUtc"] = FirestoreValues.FromDateTime(t.CreatedAtUtc),
        ["updatedAtUtc"] = FirestoreValues.FromDateTime(t.UpdatedAtUtc),
        ["updatedByUserId"] = FirestoreValues.FromULong(t.UpdatedByUserId),
        ["updatedByUsername"] = t.UpdatedByUsername
    };

    private static ProtocolTemplate FromDocument(DocumentSnapshot doc)
    {
        var template = new ProtocolTemplate
        {
            Id = doc.Id,
            Name = FirestoreValues.GetString(doc, "name", doc.Id),
            Description = FirestoreValues.GetNullableString(doc, "description"),
            Opens = FirestoreValues.GetString(doc, "opens", ProtocolTemplate.DefaultOpens),
            CreatedAtUtc = FirestoreValues.GetNullableDateTime(doc, "createdAtUtc"),
            UpdatedAtUtc = FirestoreValues.GetNullableDateTime(doc, "updatedAtUtc"),
            UpdatedByUserId = FirestoreValues.GetNullableULong(doc, "updatedByUserId"),
            UpdatedByUsername = FirestoreValues.GetNullableString(doc, "updatedByUsername")
        };

        if (doc.TryGetValue<List<Dictionary<string, object>>>("fields", out var fields) && fields != null)
        {
            template.Fields = fields.Select(f => new TemplateField
            {
                Key = Text(f, "key") ?? string.Empty,
                Label = Text(f, "label") ?? string.Empty,
                Required = f.TryGetValue("required", out var r) && r is true
            }).Where(f => f.Key.Length > 0).ToList();
        }

        if (doc.TryGetValue<List<Dictionary<string, object>>>("steps", out var steps) && steps != null)
        {
            template.Steps = steps.Select(ReadStep).ToList();
        }

        return template;
    }

    private static TemplateStep ReadStep(Dictionary<string, object> s)
    {
        var step = new TemplateStep
        {
            Id = Text(s, "id") ?? TemplateStep.NewId(),
            Title = Text(s, "title") ?? string.Empty,
            Phase = Text(s, "phase"),
            Owner = Text(s, "owner") ?? string.Empty,
            Due = Text(s, "due") ?? "0",
            Where = Text(s, "where"),
            Instructions = Text(s, "instructions"),
            AssetsUrl = Text(s, "assetsUrl"),
            ExampleUrl = Text(s, "exampleUrl")
        };

        if (s.TryGetValue("posts", out var postsValue) && postsValue is IDictionary<string, object> posts)
        {
            foreach (var (platform, postValue) in posts)
            {
                if (postValue is not IDictionary<string, object> post) continue;

                var template = new PostTemplate();
                if (post.TryGetValue("fields", out var fieldsValue) && fieldsValue is IDictionary<string, object> fields)
                {
                    foreach (var (key, value) in fields)
                    {
                        if (value is string text) template.Fields[key] = text;
                    }
                }

                if (post.TryGetValue("checklist", out var listValue) && listValue is IEnumerable<object> items)
                {
                    template.Checklist = items.OfType<string>().ToList();
                }

                step.Posts[platform] = template;
            }
        }

        return step;
    }

    private static string? Text(IDictionary<string, object> map, string key) =>
        map.TryGetValue(key, out var value) ? value as string : null;

    #endregion
}
