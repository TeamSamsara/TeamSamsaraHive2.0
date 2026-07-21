using Microsoft.EntityFrameworkCore;
using TeamSamsara.Hive.Bot.Streams.Models.Entities;
using TeamSamsara.Hive.Core.Database.Abstractions;

namespace TeamSamsara.Hive.Bot.Streams.Data;

// Stream tracking system's database context
// Manages streams.db - deliberately separate from Ticket/Configuration DBs
// so this feature has zero coupling to those systems
public class StreamDbContext : BaseDbContext
{
    public DbSet<TrackedStreamEntity> TrackedStreams { get; set; }

    public StreamDbContext(DbContextOptions<StreamDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // Apply centralized policies first

        modelBuilder.Entity<TrackedStreamEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.TwitchUsername)
                .IsUnique();

            entity.Property(e => e.TwitchUsername)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.DisplayName)
                .HasMaxLength(50);

            entity.Property(e => e.AddedByUsername)
                .IsRequired()
                .HasMaxLength(100);
        });
    }
}