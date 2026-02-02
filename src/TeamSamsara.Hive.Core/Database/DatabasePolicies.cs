using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Core.Database.Policies;

// Centralized database policies applied to all system DbContexts
// Write once, apply everywhere - update once, all systems benefit
public static class DatabasePolicies
{
    #region Connection & Performance Policies
    
    // Apply connection pooling optimization policy
    // Disables thread safety checks for better performance (safe with DI scopes)
    public static void ApplyConnectionPooling(DbContextOptionsBuilder options)
    {
        options.EnableThreadSafetyChecks(false);
    }
    
    // Apply query optimization policy
    // Defaults to no-tracking for better performance (systems can opt-in to tracking when needed)
    public static void ApplyQueryOptimization(DbContextOptionsBuilder options)
    {
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }
    
    // Apply logging policy
    // Development: log everything with sensitive data
    // Production: minimal logging
    public static void ApplyLogging(DbContextOptionsBuilder options)
    {
        #if DEBUG
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
        #endif
    }
    
    #endregion
    
    #region Model Configuration Policies
    
    // Apply naming convention policy
    // Rule: Entity classes become plural table names (ConfigurationEntity -> Configurations)
    public static void ApplyNamingConventions(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // Entity name + "s" for plural tables
            var tableName = entity.ClrType.Name;
            
            // Remove "Entity" suffix if present, then pluralize
            if (tableName.EndsWith("Entity"))
            {
                tableName = tableName.Substring(0, tableName.Length - 6);
            }
            
            entity.SetTableName(tableName + "s");
        }
    }
    
    // Apply indexing strategy policy
    // Automatically creates indexes for common patterns
    public static void ApplyIndexingStrategy(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // Rule: Properties named "Key" get unique index
            var keyProperty = entity.FindProperty("Key");
            if (keyProperty != null)
            {
                var index = entity.FindIndex(keyProperty);
                if (index == null)
                {
                    var newIndex = entity.AddIndex(keyProperty);
                    newIndex.IsUnique = true;
                }
            }
            
            // Rule: DateTime properties get indexes (for sorting/filtering)
            var dateProperties = entity.GetProperties()
                .Where(p => p.ClrType == typeof(DateTime) || p.ClrType == typeof(DateTime?));
            
            foreach (var dateProp in dateProperties)
            {
                var existingIndex = entity.FindIndex(dateProp);
                if (existingIndex == null)
                {
                    entity.AddIndex(dateProp);
                }
            }
        }
    }
    
    // Apply entity default configuration policy
    // Sets consistent defaults for all entities
    public static void ApplyEntityDefaults(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // Rule: String properties default to max 500 chars (unless specified)
            var stringProperties = entity.GetProperties()
                .Where(p => p.ClrType == typeof(string));
            
            foreach (var stringProp in stringProperties)
            {
                if (stringProp.GetMaxLength() == null)
                {
                    stringProp.SetMaxLength(500);
                }
            }
        }
    }
    
    #endregion
    
    #region Audit Policies
    
    // Apply audit timestamp policy
    // Automatically sets CreatedAtUtc and UpdatedAtUtc on all BaseEntity entities
    public static void ApplyAuditTimestamps(ChangeTracker changeTracker)
    {
        var entries = changeTracker.Entries()
            .Where(e => e.Entity is BaseEntity && 
                       (e.State == EntityState.Added || e.State == EntityState.Modified));
        
        var now = DateTime.UtcNow;
        
        foreach (var entry in entries)
        {
            var entity = (BaseEntity)entry.Entity;
            
            // Always update ModifiedAtUtc (UpdatedAtUtc in BaseEntity)
            entity.UpdatedAtUtc = now;
            
            // Only set CreatedAtUtc for new entities
            if (entry.State == EntityState.Added)
            {
                entity.CreatedAtUtc = now;
            }
        }
    }
    
    #endregion
    
    #region Caching Policies
    
    // Centralized caching duration policies
    // Each system can use appropriate cache duration for its data
    public static class CachingPolicy
    {
        // Default cache duration for most data
        public static TimeSpan DefaultCacheDuration => TimeSpan.FromMinutes(5);
        
        // Configuration data changes rarely - cache longer
        public static TimeSpan ConfigurationCacheDuration => TimeSpan.FromMinutes(30);
        
        // Analytics data changes frequently - cache shorter
        public static TimeSpan AnalyticsCacheDuration => TimeSpan.FromMinutes(2);
        
        // Ticket data changes very frequently - cache very short
        public static TimeSpan TicketCacheDuration => TimeSpan.FromSeconds(30);
        
        // Notification logs are append-only - can cache longer
        public static TimeSpan NotificationCacheDuration => TimeSpan.FromMinutes(10);
    }
    
    #endregion
}