using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Configuration.Models.Entities;
using TeamSamsara.Hive.Core.Database.Abstractions;

namespace TeamSamsara.Hive.Bot.Configuration.Data;

// Configuration System's query optimizer
// Provides optimized query patterns for configuration data access
public class ConfigurationQueryOptimizer : BaseQueryOptimizer
{
    public override string ServiceName => "ConfigurationQueryOptimizer";
    
    #region Constructor
    
    public ConfigurationQueryOptimizer(ILogger<ConfigurationQueryOptimizer> logger, IConfiguration configuration) 
        : base(logger, configuration)
    {
    }
    
    #endregion
    
    #region Compiled Queries
    
    // Compiled query for GetByKey (hot path - called frequently)
    // Pre-compiled for maximum performance
    private static readonly Func<ConfigurationDbContext, string, Task<ConfigurationEntity?>> 
        GetByKeyCompiled = EF.CompileAsyncQuery(
            (ConfigurationDbContext ctx, string key) =>
                ctx.Configurations.FirstOrDefault(c => c.Key == key));
    
    // Compiled query for checking if key exists (hot path)
    private static readonly Func<ConfigurationDbContext, string, Task<bool>> 
        ExistsByKeyCompiled = EF.CompileAsyncQuery(
            (ConfigurationDbContext ctx, string key) =>
                ctx.Configurations.Any(c => c.Key == key));
    
    #endregion
    
    #region Optimized Query Methods
    
    // Get configuration by key (optimized with compiled query)
    // This is the most frequent operation in the Configuration System
    public async Task<ConfigurationEntity?> GetByKeyOptimizedAsync(
        ConfigurationDbContext context, 
        string key)
    {
        return await ExecuteWithLoggingAsync(
            "GetByKey",
            async () => await GetByKeyCompiled(context, key),
            threshold: TimeSpan.FromMilliseconds(50));
    }
    
    // Check if configuration key exists (optimized with compiled query)
    public async Task<bool> ExistsByKeyOptimizedAsync(
        ConfigurationDbContext context,
        string key)
    {
        return await ExecuteWithLoggingAsync(
            "ExistsByKey",
            async () => await ExistsByKeyCompiled(context, key),
            threshold: TimeSpan.FromMilliseconds(30));
    }
    
    // Get all configurations with standard optimizations
    // Less frequent operation, standard optimization is sufficient
    public IQueryable<ConfigurationEntity> GetAllOptimized(ConfigurationDbContext context)
    {
        var query = context.Configurations.AsQueryable();
        return ApplyStandardOptimizations(query);
    }
    
    // Get configurations by key pattern (e.g., all keys starting with "Discord")
    // Useful for getting related configurations
    public IQueryable<ConfigurationEntity> GetByKeyPatternOptimized(
        ConfigurationDbContext context,
        string keyPattern)
    {
        var query = context.Configurations
            .Where(c => EF.Functions.Like(c.Key, keyPattern));
        
        return ApplyNoTracking(query);
    }
    
    // Get recently updated configurations
    // Useful for auditing and debugging
    public IQueryable<ConfigurationEntity> GetRecentlyUpdatedOptimized(
        ConfigurationDbContext context,
        int limit = 10)
    {
        return GetRecentItems(
            context.Configurations,
            c => c.LastUpdatedUtc,
            limit);
    }
    
    #endregion
}