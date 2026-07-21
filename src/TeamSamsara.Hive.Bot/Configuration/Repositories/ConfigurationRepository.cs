using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Configuration.Data;
using TeamSamsara.Hive.Bot.Configuration.Models.Entities;
using TeamSamsara.Hive.Core.Abstractions;
using TeamSamsara.Hive.Core.Database.Policies;

namespace TeamSamsara.Hive.Bot.Configuration.Repositories;

// Repository implementation for Configuration data access
// Uses ConfigurationDbContext and ConfigurationQueryOptimizer for optimized database operations
public class ConfigurationRepository : BaseRepository, IConfigurationRepository
{
    private readonly ConfigurationDbContext _context;
    private readonly ConfigurationQueryOptimizer _queryOptimizer;
    private readonly IMemoryCache _cache;
    
    public override string RepositoryName => "ConfigurationRepository";
    
    #region Constructor
    
    public ConfigurationRepository(
        ConfigurationDbContext context,
        ConfigurationQueryOptimizer queryOptimizer,
        IMemoryCache cache,
        ILogger<ConfigurationRepository> logger) : base(logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _queryOptimizer = queryOptimizer ?? throw new ArgumentNullException(nameof(queryOptimizer));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }
    
    #endregion
    
    #region Public Methods
    
    // Get a configuration by key (with caching)
    public async Task<ConfigurationEntity?> GetByKeyAsync(string key)
    {
        try
        {
            var cacheKey = $"config:{key}";
            
            // Check cache first
            if (_cache.TryGetValue(cacheKey, out ConfigurationEntity? cached))
            {
                LogDebug("Cache hit: {Key}", key);
                return cached;
            }
            
            // Use optimized compiled query
            var entity = await _queryOptimizer.GetByKeyOptimizedAsync(_context, key);
            
            // Cache the result using centralized caching policy
            if (entity != null)
            {
                _cache.Set(cacheKey, entity, DatabasePolicies.CachingPolicy.ConfigurationCacheDuration);
                LogDebug("Cache set: {Key}", key);
            }
            
            return entity;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get configuration by key: {Key}", key);
            throw;
        }
    }
    
    // Get all configurations (with optimization)
    public async Task<List<ConfigurationEntity>> GetAllAsync()
    {
        try
        {
            var query = _queryOptimizer.GetAllOptimized(_context);
            return await query.OrderBy(c => c.Key).ToListAsync();
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get all configurations");
            throw;
        }
    }
    
    // Insert or update a configuration (upsert)
    public async Task UpsertAsync(ConfigurationEntity entity)
    {
        try
        {
            var existing = await _context.Configurations
                .FirstOrDefaultAsync(c => c.Key == entity.Key);
            
            if (existing != null)
            {
                // Update existing
                existing.Value = entity.Value;
                existing.DisplayName = entity.DisplayName;
                existing.LastUpdatedUtc = entity.LastUpdatedUtc;
                existing.LastUpdatedByUserId = entity.LastUpdatedByUserId;
                existing.LastUpdatedByUsername = entity.LastUpdatedByUsername;
                
                _context.Update(existing);
            }
            else
            {
                // Insert new
                await _context.Configurations.AddAsync(entity);
            }
            
            // SaveChangesAsync applies centralized audit policy automatically
            await _context.SaveChangesAsync();
            
            // Invalidate cache
            var cacheKey = $"config:{entity.Key}";
            _cache.Remove(cacheKey);
            LogDebug("Cache invalidated: {Key}", entity.Key);
            
            LogInfo("Configuration {Key} upserted successfully", entity.Key);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to upsert configuration: {Key}", entity.Key);
            throw;
        }
    }
    
    // Delete a configuration by key
    public async Task DeleteAsync(string key)
    {
        try
        {
            var entity = await _context.Configurations
                .FirstOrDefaultAsync(c => c.Key == key);
            
            if (entity != null)
            {
                _context.Configurations.Remove(entity);
                await _context.SaveChangesAsync();
                
                // Invalidate cache
                var cacheKey = $"config:{key}";
                _cache.Remove(cacheKey);
                LogDebug("Cache invalidated: {Key}", key);
                
                LogInfo("Configuration {Key} deleted successfully", key);
            }
            else
            {
                LogWarning("Configuration {Key} not found for deletion", key);
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to delete configuration: {Key}", key);
            throw;
        }
    }
    
    // Check if a configuration key exists (optimized with compiled query)
    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            return await _queryOptimizer.ExistsByKeyOptimizedAsync(_context, key);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to check if configuration exists: {Key}", key);
            throw;
        }
    }
    
    #endregion
}