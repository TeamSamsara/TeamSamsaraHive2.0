namespace TeamSamsara.Hive.Bot.Configuration.Models.DTOs;

// Individual configuration item for export
internal class ConfigItemDto
{
    public string Key { get; set; }                          // Configuration key
    public string Value { get; set; }                        // Configuration value
    public string? DisplayName { get; set; }                 // Human-readable name
    public DateTime LastUpdated { get; set; }                // When this was last updated
    
    // Constructor
    public ConfigItemDto()
    {
        Key = string.Empty;
        Value = string.Empty;
    }
}