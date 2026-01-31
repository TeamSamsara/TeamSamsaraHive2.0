namespace TeamSamsara.Hive.Core.Abstractions;

// Base class for all DTOs (Data Transfer Objects)
// Used for API request/response objects
// Extends BaseModel and adds validation tracking
public abstract class BaseDto : BaseModel
{
    // Indicates whether this DTO passed validation
    public bool IsValid { get; set; } = true;
    
    // Validation error message if IsValid is false
    public string? ErrorMessage { get; set; }
}