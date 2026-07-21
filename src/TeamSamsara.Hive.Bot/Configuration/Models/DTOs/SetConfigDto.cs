using TeamSamsara.Hive.Bot.Configuration.Models.Enums;
using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Configuration.Models.DTOs;

// DTO for setting a configuration key value
// Used when admin sets a channel/role/category via slash commands

public class SetConfigDto : BaseDto
{
    public ConfigKey Key {get;set;} // which configuration key to set 
    public ulong ValueId {get;set;} // The Discord ID 
    public string DisplayName {get;set;} // Human readable name
    public ulong UpdatedByUserId {get;set;} // Who is making this change 
    public string UpdatedByUsername {get;set;} // Username of who is making this change

    // constructor
    public SetConfigDto()
    {
        DisplayName = string.Empty;
        UpdatedByUsername = string.Empty;
    }

}