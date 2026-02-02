namespace TeamSamsara.Hive.Bot.Configuration.Models.DTOs;

// DTO for importing configuration from JSON file
// Used when admin runs /config import
internal class ImportConfigDto
{
    public DateTime ExportDate { get; set; }                 // When the export was originally created
    public string ExportedBy { get; set; }                   // Username who created the export
    public ulong ExportedByUserId { get; set; }              // User ID who created the export
    public List<ConfigItemDto> Configurations { get; set; }  // Configuration items to import
    
    // Constructor
    public ImportConfigDto()
    {
        ExportedBy = string.Empty;
        Configurations = new List<ConfigItemDto>();
    }
}