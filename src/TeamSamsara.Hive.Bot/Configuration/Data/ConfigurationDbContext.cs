using Microsoft.EntityFrameworkCore;
using TeamSamsara.Hive.Bot.Configuration.Models.Entities;
using TeamSamsara.Hive.Core.Database.Abstractions;

namespace TeamSamsara.Hive.Bot.Configuration.Data;

// Configuration System's database context
// Inherits centralized policies from BaseDbContext
// Manages configuration.db
public class ConfigurationDbContext : BaseDbContext
{
    #region DbSets
    
    // Configuration entities
    public DbSet<ConfigurationEntity> Configurations { get; set; }
    
    #endregion
    
    #region Constructor
    
    public ConfigurationDbContext(DbContextOptions<ConfigurationDbContext> options) 
        : base(options)
    {
    }
    
    #endregion
    
    #region Model Configuration
    
    // Apply Configuration System-specific schema
    // Base policies are applied automatically by BaseDbContext
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Apply centralized policies first
        
        // Configuration-specific schema
        modelBuilder.Entity<ConfigurationEntity>(entity =>
        {
            // Primary key
            entity.HasKey(e => e.Id);
            
            // Unique index on Key (in addition to centralized indexing policy)
            entity.HasIndex(e => e.Key)
                .IsUnique();
            
            // Column configurations
            entity.Property(e => e.Key)
                .IsRequired()
                .HasMaxLength(100);
            
            entity.Property(e => e.Value)
                .IsRequired()
                .HasMaxLength(255);
            
            entity.Property(e => e.DisplayName)
                .HasMaxLength(255);
            
            entity.Property(e => e.LastUpdatedByUsername)
                .IsRequired()
                .HasMaxLength(100);
            
            // Index on LastUpdatedUtc for queries
            entity.HasIndex(e => e.LastUpdatedUtc);
        });
    }
    
    #endregion
}