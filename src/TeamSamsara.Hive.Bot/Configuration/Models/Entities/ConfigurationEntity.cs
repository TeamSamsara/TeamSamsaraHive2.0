using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Configuration.Models.Entities;

// Entity representing a single configuration key-value pair in the database
// Table : Configuration
public class ConfigurationEntity : BaseEntity
{
    #region Core Properties
    public string Key {get; set;}
    public string Value {get;set;}
    public string? DisplayName {get;set;}
    #endregion

    #region Audit Trail
    public DateTime LastUpdatedUtc {get;set;}
    public ulong LastUpdatedByUserId {get;set;}
    public string LastUpdatedByUsername {get;set;}
    #endregion

    // Constructor
    public ConfigurationEntity()
    {
        Key = string.Empty;
        Value = string.Empty;
        LastUpdatedByUsername = string.Empty;
    }
}