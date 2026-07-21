using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Configuration.Models.ViewModels;

// ViewModel for displaying configuration in Discord embeds
// Used when formatting /config show responses
public class ConfigurationViewModel : BaseViewModel
{
    public string Key { get; set; }                          // Configuration key name
    public string Value { get; set; }                        // Configuration value (ID)
    public string DisplayName { get; set; }                  // Human-readable name (e.g., "#announcements")
    public DateTime LastUpdated { get; set; }                // When last updated
    public string LastUpdatedBy { get; set; }                // Who last updated
    
    // Constructor
    public ConfigurationViewModel()
    {
        Key = string.Empty;
        Value = string.Empty;
        DisplayName = string.Empty;
        LastUpdatedBy = string.Empty;
    }
}