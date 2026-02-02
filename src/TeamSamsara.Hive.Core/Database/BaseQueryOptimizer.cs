using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Core.Database.Abstractions;

// Base class for system-specific query optimizers
// Provides centralized query optimization patterns that systems can extend
public abstract class BaseQueryOptimizer : BaseService
{
    #region Constructor
    
    protected BaseQueryOptimizer(ILogger logger, IConfiguration configuration) 
        : base(logger, configuration)
    {
    }
    
    #endregion
    
    #region Compiled Query Cache
    
    // Shared cache for compiled queries across all systems
    // Compiled queries are pre-compiled LINQ expressions for better performance on hot paths
    protected static readonly ConcurrentDictionary<string, Delegate> CompiledQueryCache = new();
    
    #endregion
    
    #region Standard Optimizations
    
    // Apply standard no-tracking optimization
    // Use for read-only queries that don't need change tracking
    protected IQueryable<T> ApplyNoTracking<T>(IQueryable<T> query) where T : class
    {
        return query.AsNoTracking();
    }
    
    // Apply split query optimization
    // Use for queries with multiple includes to avoid cartesian explosion
    protected IQueryable<T> ApplySplitQuery<T>(IQueryable<T> query) where T : class
    {
        return query.AsSplitQuery();
    }
    
    // Apply standard optimizations (no-tracking + split query)
    // Use as default for most read queries
    protected IQueryable<T> ApplyStandardOptimizations<T>(IQueryable<T> query) where T : class
    {
        return query
            .AsNoTracking()
            .AsSplitQuery();
    }
    
    #endregion
    
    #region Pagination
    
    // Apply pagination to query
    // Page numbers are 1-indexed (page 1 = first page)
    protected IQueryable<T> ApplyPagination<T>(IQueryable<T> query, int page, int pageSize) where T : class
    {
        if (page < 1)
            page = 1;
        
        if (pageSize < 1)
            pageSize = 10;
        
        return query
            .Skip((page - 1) * pageSize)
            .Take(pageSize);
    }
    
    // Get total count and paginated results in one operation
    // Returns tuple of (totalCount, pagedResults)
    protected async Task<(int totalCount, List<T> results)> GetPagedResultsAsync<T>(
        IQueryable<T> query, 
        int page, 
        int pageSize) where T : class
    {
        var totalCount = await query.CountAsync();
        var results = await ApplyPagination(query, page, pageSize).ToListAsync();
        
        return (totalCount, results);
    }
    
    #endregion
    
    #region Common Query Patterns
    
    // Get recent items (ordered by date descending)
    // Common pattern: get last N items
    protected IQueryable<T> GetRecentItems<T>(
        IQueryable<T> query, 
        System.Linq.Expressions.Expression<Func<T, DateTime>> dateSelector,
        int limit) where T : class
    {
        return query
            .OrderByDescending(dateSelector)
            .Take(limit);
    }
    
    // Check if entity exists by predicate
    // Optimized to stop as soon as match is found
    protected async Task<bool> ExistsAsync<T>(
        IQueryable<T> query,
        System.Linq.Expressions.Expression<Func<T, bool>> predicate) where T : class
    {
        return await query.AnyAsync(predicate);
    }
    
    // Get single entity or null (optimized)
    // Use when expecting 0 or 1 results
    protected async Task<T?> GetSingleOrDefaultAsync<T>(
        IQueryable<T> query,
        System.Linq.Expressions.Expression<Func<T, bool>> predicate) where T : class
    {
        return await query
            .AsNoTracking()
            .FirstOrDefaultAsync(predicate);
    }
    
    #endregion
    
    #region Query Hints
    
    // Query hints for optimization decisions
    // Systems can pass these to control optimization behavior
    public class QueryHints
    {
        public bool UseNoTracking { get; set; } = true;
        public bool UseSplitQuery { get; set; } = false;
        public bool UseCompiledQuery { get; set; } = false;
        public int? CacheDurationSeconds { get; set; }
    }
    
    // Apply query hints to a query
    protected IQueryable<T> ApplyQueryHints<T>(IQueryable<T> query, QueryHints hints) where T : class
    {
        if (hints.UseNoTracking)
            query = query.AsNoTracking();
        
        if (hints.UseSplitQuery)
            query = query.AsSplitQuery();
        
        return query;
    }
    
    #endregion
    
    #region Performance Logging
    
    // Log slow query warning
    // Call this when a query takes longer than expected
    protected void LogSlowQuery(string queryName, TimeSpan duration, TimeSpan threshold)
    {
        if (duration > threshold)
        {
            LogWarning("Slow query detected: {QueryName} took {Duration}ms (threshold: {Threshold}ms)",
                queryName, duration.TotalMilliseconds, threshold.TotalMilliseconds);
        }
    }
    
    // Execute query with performance logging
    // Automatically logs if query is slow
    protected async Task<T> ExecuteWithLoggingAsync<T>(
        string queryName,
        Func<Task<T>> queryFunc,
        TimeSpan? threshold = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var result = await queryFunc();
            sw.Stop();
            
            var defaultThreshold = threshold ?? TimeSpan.FromMilliseconds(100);
            LogSlowQuery(queryName, sw.Elapsed, defaultThreshold);
            
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogError(ex, "Query failed: {QueryName} after {Duration}ms", queryName, sw.Elapsed.TotalMilliseconds);
            throw;
        }
    }
    
    #endregion
}