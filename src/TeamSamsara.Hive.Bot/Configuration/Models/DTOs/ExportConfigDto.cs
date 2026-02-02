using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Configuration.Models.DTOs;

// DTO for exporting configuration to JSON file
// Used when admin runs /config export
internal class ExportConfigDto : BaseDto
{
    public DateTime ExportDate { get; set; }                 // When this export was created
    public string ExportedBy { get; set; }                   // Username who exported
    public ulong ExportedByUserId { get; set; }              // User ID who exported
    public List<ConfigItemDto> Configurations { get; set; }  // All configuration items
    
    // Constructor
    public ExportConfigDto()
    {
        ExportedBy = string.Empty;
        Configurations = new List<ConfigItemDto>();
    }
}