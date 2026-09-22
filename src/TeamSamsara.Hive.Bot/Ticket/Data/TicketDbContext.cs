using Microsoft.EntityFrameworkCore;
using TeamSamsara.Hive.Bot.Tickets.Models.Entities;
using TeamSamsara.Hive.Core.Database.Abstractions;

namespace TeamSamsara.Hive.Bot.Tickets.Data;

// Database context for the Ticket System
// Inherits centralized policies from BaseDbContext
// Manages tickets.db database file
public class TicketDbContext : BaseDbContext
{
    #region DbSets
    
    // Ticket entities
    public DbSet<TicketEntity> Tickets { get; set; } = null!;
    
    // Status transition audit trail
    public DbSet<StatusTransitionEntity> StatusTransitions { get; set; } = null!;
    
    #endregion
    
    #region Constructor
    
    public TicketDbContext(DbContextOptions<TicketDbContext> options) 
        : base(options)
    {
    }
    
    #endregion
    
    #region Model Configuration
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply base policies first (from BaseDbContext)
        base.OnModelCreating(modelBuilder);
        
        // Configure TicketEntity
        modelBuilder.Entity<TicketEntity>(entity =>
        {
            // Primary key
            entity.HasKey(e => e.Id);
            
            // Unique index on SubmissionId (ticket ID like "TICKET-001")
            entity.HasIndex(e => e.SubmissionId)
                .IsUnique();
            
            // Index on Status for filtering
            entity.HasIndex(e => e.Status);
            
            // Index on SubmittedByUserId for user-specific queries
            entity.HasIndex(e => e.SubmittedByUserId);
            
            // Index on AssignedToUserId for staff workload queries
            entity.HasIndex(e => e.AssignedToUserId);
            
            // Index on Priority for priority-based filtering
            entity.HasIndex(e => e.Priority);
            
            // Index on Category for category-based filtering
            entity.HasIndex(e => e.Category);
            
            // Index on SubmittedAtUtc for chronological sorting
            entity.HasIndex(e => e.SubmittedAtUtc);
            
            // Composite index for common query: status + priority
            entity.HasIndex(e => new { e.Status, e.Priority });
            
            // Composite index for assigned tickets: assigned user + status
            entity.HasIndex(e => new { e.AssignedToUserId, e.Status });
            
            // Column max lengths
            entity.Property(e => e.SubmissionId)
                .HasMaxLength(50)
                .IsRequired();
            
            entity.Property(e => e.Title)
                .HasMaxLength(100)
                .IsRequired();
            
            entity.Property(e => e.Description)
                .HasMaxLength(2000)
                .IsRequired();
            
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsRequired();
            
            entity.Property(e => e.SubmittedByUsername)
                .HasMaxLength(100)
                .IsRequired();
            
            entity.Property(e => e.AssignedToUsername)
                .HasMaxLength(100);
            
            entity.Property(e => e.ResolvedByUsername)
                .HasMaxLength(100);
            
            entity.Property(e => e.Resolution)
                .HasMaxLength(2000);
            
            // Enum storage as integers
            entity.Property(e => e.Category)
                .HasConversion<int>();
            
            entity.Property(e => e.Priority)
                .HasConversion<int>();
            
            entity.Property(e => e.Type)
                .HasConversion<int>();
        });
        
        // Configure StatusTransitionEntity
        modelBuilder.Entity<StatusTransitionEntity>(entity =>
        {
            // Primary key
            entity.HasKey(e => e.Id);
            
            // Index on SubmissionId for fast lookup
            entity.HasIndex(e => e.SubmissionId);
            
            // Index on TransitionedAtUtc for chronological queries
            entity.HasIndex(e => e.TransitionedAtUtc);
            
            // Composite index for submission history queries
            entity.HasIndex(e => new { e.SubmissionId, e.TransitionedAtUtc });
            
            // Column max lengths
            entity.Property(e => e.SubmissionId)
                .HasMaxLength(50)
                .IsRequired();
            
            entity.Property(e => e.SubmissionType)
                .HasMaxLength(50)
                .IsRequired();
            
            entity.Property(e => e.FromStatus)
                .HasMaxLength(50)
                .IsRequired();
            
            entity.Property(e => e.ToStatus)
                .HasMaxLength(50)
                .IsRequired();
            
            entity.Property(e => e.TransitionedByUsername)
                .HasMaxLength(100)
                .IsRequired();
            
            entity.Property(e => e.ReasonCode)
                .HasMaxLength(100);
        });
    }
    
    #endregion
}