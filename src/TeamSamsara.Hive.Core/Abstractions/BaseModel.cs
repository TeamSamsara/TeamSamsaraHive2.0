namespace TeamSamsara.Hive.Core.Abstractions;

// Root base class for all models in the application
// Provide common timestamp for all entities, DTOs, and view models

public abstract class BaseModel
{
    // Timestamp for when this model was created or last modified (UTC)
    public DateTime TimeStampUtc {get;set;} = DateTime.UtcNow;
}