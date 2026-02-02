using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Configuration.Models.Entities;
using TeamSamsara.Hive.Bot.Configuration.Models.Enums;
using TeamSamsara.Hive.Bot.Configuration.Repositories;
using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Configuration.Services;

// Service for retrieving configuration values from the database
// Used by all systems to get channel IDs, role IDs, and category IDs
public class GetConfigValueService : BaseService
{
    private readonly IConfigurationRepository _repository;
    
    public override string ServiceName => "GetConfigValue";
    
    #region Constructor
    
    public GetConfigValueService(
        IConfigurationRepository repository,
        ILogger<GetConfigValueService> logger,
        IConfiguration configuration)
        : base(logger, configuration)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }
    
    #endregion
    
    #region Public Methods
    
    // Get a channel ID by configuration key
    public async Task<ulong?> GetChannelIdAsync(ConfigKey key)
    {
        try
        {
            var config = await _repository.GetByKeyAsync(key.ToString());
            if (config == null || string.IsNullOrEmpty(config.Value))
            {
                LogWarning("Configuration key {Key} not found or has no value", key);
                return null;
            }
            
            if (!ulong.TryParse(config.Value, out var channelId))
            {
                LogWarning("Configuration key {Key} has invalid value: {Value}", key, config.Value);
                return null;
            }
            
            return channelId;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get channel ID for key: {Key}", key);
            return null;
        }
    }
    
    // Get a role ID by configuration key
    public async Task<ulong?> GetRoleIdAsync(ConfigKey key)
    {
        try
        {
            var config = await _repository.GetByKeyAsync(key.ToString());
            if (config == null || string.IsNullOrEmpty(config.Value))
            {
                LogWarning("Configuration key {Key} not found or has no value", key);
                return null;
            }
            
            if (!ulong.TryParse(config.Value, out var roleId))
            {
                LogWarning("Configuration key {Key} has invalid value: {Value}", key, config.Value);
                return null;
            }
            
            return roleId;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get role ID for key: {Key}", key);
            return null;
        }
    }
    
    // Get a category ID by configuration key
    public async Task<ulong?> GetCategoryIdAsync(ConfigKey key)
    {
        try
        {
            var config = await _repository.GetByKeyAsync(key.ToString());
            if (config == null || string.IsNullOrEmpty(config.Value))
            {
                LogWarning("Configuration key {Key} not found or has no value", key);
                return null;
            }
            
            if (!ulong.TryParse(config.Value, out var categoryId))
            {
                LogWarning("Configuration key {Key} has invalid value: {Value}", key, config.Value);
                return null;
            }
            
            return categoryId;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get category ID for key: {Key}", key);
            return null;
        }
    }
    
    // Get all configurations
    public async Task<List<ConfigurationEntity>> GetAllAsync()
    {
        try
        {
            return await _repository.GetAllAsync();
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get all configurations");
            return new List<ConfigurationEntity>();
        }
    }
    
    // Get configurations by category
    public async Task<List<ConfigurationEntity>> GetByCategoryAsync(ConfigCategory category)
    {
        try
        {
            var allConfigs = await _repository.GetAllAsync();
            var keysForCategory = GetKeysForCategory(category);
            
            return allConfigs
                .Where(c => keysForCategory.Contains(c.Key))
                .ToList();
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get configurations for category: {Category}", category);
            return new List<ConfigurationEntity>();
        }
    }
    
    #endregion
    
    #region Private Methods
    
    // Get list of configuration keys for a specific category
    private List<string> GetKeysForCategory(ConfigCategory category)
    {
        return category switch
        {
            ConfigCategory.NotificationChannels => new List<string>
            {
                ConfigKey.StreamNotificationChannelId.ToString(),
                ConfigKey.UrgentTicketChannelId.ToString(),
                ConfigKey.AdminChannelId.ToString(),
                ConfigKey.AnalyticsReportChannelId.ToString(),
                ConfigKey.TicketLogChannelId.ToString()
            },
            
            ConfigCategory.TicketCategories => new List<string>
            {
                ConfigKey.ActiveTicketsCategoryId.ToString(),
                ConfigKey.ClosedTicketsCategoryId.ToString()
            },
            
            ConfigCategory.VoiceStatsChannels => new List<string>
            {
                ConfigKey.UrgentStatsChannelId.ToString(),
                ConfigKey.HighStatsChannelId.ToString(),
                ConfigKey.NormalStatsChannelId.ToString(),
                ConfigKey.EscalatedStatsChannelId.ToString(),
                ConfigKey.OldestStatsChannelId.ToString()
            },
            
            ConfigCategory.Roles => new List<string>
            {
                ConfigKey.AdminRoleId.ToString(),
                ConfigKey.ModeratorRoleId.ToString(),
                ConfigKey.StaffRoleId.ToString(),
                ConfigKey.StreamNotificationRoleId.ToString()
            },
            
            _ => new List<string>()
        };
    }
    
    #endregion
}