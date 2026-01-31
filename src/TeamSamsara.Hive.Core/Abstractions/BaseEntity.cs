namespace TeamSamsara.Hive.Core.Abstractions;

// Base class for all database entities
// Extends BaseModel and adds identity and audit tracking
public abstract class BaseEntity : BaseModel
{
    // Unique identifier for this entity
    public Guid Id { get; set; }
    
    // When this entity was created (UTC)
    public DateTime CreatedAtUtc { get; set; }
    
    // When this entity was last updated (UTC), null if never updated
    public DateTime? UpdatedAtUtc { get; set; }
    
    // Constructor auto-generates ID and sets creation timestamp
    protected BaseEntity()
    {
        Id = Guid.NewGuid();
        CreatedAtUtc = DateTime.UtcNow;
    }
}