using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using TeamSamsara.Hive.Core.Abstractions;
using TeamSamsara.Hive.Core.Database.Policies;

namespace TeamSamsara.Hive.Core.Database.Abstractions;

// Base class for all system DbContexts
// Applies centralized database policies: connection pooling, query optimization, naming conventions, audit timestamps
public abstract class BaseDbContext : DbContext
{
    #region Constructor
    
    protected BaseDbContext(DbContextOptions options) : base(options)
    {
    }
    
    #endregion
    
    #region Configuration
    
    // Apply centralized configuration policies to all DbContexts
    // Override in derived classes to add system-specific configuration
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        
        if (!optionsBuilder.IsConfigured)
        {
            return;
        }
        
        // Apply centralized policies
        DatabasePolicies.ApplyConnectionPooling(optionsBuilder);
        DatabasePolicies.ApplyQueryOptimization(optionsBuilder);
        DatabasePolicies.ApplyLogging(optionsBuilder);
    }
    
    // Apply centralized model conventions to all DbContexts
    // Override in derived classes to add system-specific schema
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Apply centralized conventions
        DatabasePolicies.ApplyNamingConventions(modelBuilder);
        DatabasePolicies.ApplyIndexingStrategy(modelBuilder);
        DatabasePolicies.ApplyEntityDefaults(modelBuilder);
    }
    
    #endregion
    
    #region Save Operations
    
    // Override SaveChanges to apply centralized audit policies
    // Automatically sets CreatedAtUtc and UpdatedAtUtc timestamps
    public override int SaveChanges()
    {
        ApplyAuditPolicies();
        return base.SaveChanges();
    }
    
    // Override SaveChangesAsync to apply centralized audit policies
    // Automatically sets CreatedAtUtc and UpdatedAtUtc timestamps
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditPolicies();
        return await base.SaveChangesAsync(cancellationToken);
    }
    
    #endregion
    
    #region Private Helpers
    
    // Apply centralized audit timestamp policy
    // Sets CreatedAtUtc for new entities, UpdatedAtUtc for new and modified entities
    private void ApplyAuditPolicies()
    {
        DatabasePolicies.ApplyAuditTimestamps(ChangeTracker);
    }
    
    #endregion
}