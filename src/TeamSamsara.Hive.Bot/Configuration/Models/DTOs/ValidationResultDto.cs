using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Configuration.Models.DTOs;

// DTO for configuration validation results
// Used when admin runs /config validate
public class ValidationResultDto : BaseDto
{
    public int TotalConfigurations { get; set; }             // Total number of configurations checked
    public int ValidConfigurations { get; set; }             // Number of valid configurations
    public List<InvalidConfigDto> InvalidConfigurations { get; set; }  // List of invalid configurations
    
    // Constructor
    public ValidationResultDto()
    {
        InvalidConfigurations = new List<InvalidConfigDto>();
    }
    
    // Helper property
    public bool AllValid => InvalidConfigurations.Count == 0;
}