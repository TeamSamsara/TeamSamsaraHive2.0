namespace TeamSamsara.Hive.Bot.Configuration.Models.DTOs;

// DTO for details about an invalid configuration entry
// Used in ValidationResultDto to describe what's wrong
public class InvalidConfigDto
{
    public string Key { get; set; }                          // Configuration key that's invalid
    public string Reason { get; set; }                       // Why it's invalid (e.g., "Channel does not exist")
    public DateTime LastUpdated { get; set; }                // When this config was last set
    public string LastUpdatedBy { get; set; }                // Who last updated this config
    
    // Constructor
    public InvalidConfigDto()
    {
        Key = string.Empty;
        Reason = string.Empty;
        LastUpdatedBy = string.Empty;
    }
}