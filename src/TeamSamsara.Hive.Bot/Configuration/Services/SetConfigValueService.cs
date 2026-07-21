using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using TeamSamsara.Hive.Bot.Configuration.Models.DTOs;
using TeamSamsara.Hive.Bot.Configuration.Models.Entities;
using TeamSamsara.Hive.Bot.Configuration.Models.Enums;
using TeamSamsara.Hive.Bot.Configuration.Repositories;

using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Configuration.Services;

// Service for saving configuration values to the database
// Used by ConfigurationOrchestrator when admin runs /config set-* commands
public class SetConfigValueService : BaseService
{
    private readonly IConfigurationRepository _repository;
    
    public override string ServiceName => "SetConfigValue";

    #region Constructor
    
    public SetConfigValueService(
        IConfigurationRepository repository,
        ILogger<SetConfigValueService> logger,
        IConfiguration configuration)
        : base(logger, configuration)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }
    
    #endregion

    #region Public API
    
    // Set a channel configuration
    public async Task SetChannelAsync(SetConfigDto dto)
    {
        try
        {
            var entity = new ConfigurationEntity
            {
                Key = dto.Key.ToString(),
                Value = dto.ValueId.ToString(),
                DisplayName = dto.DisplayName,
                LastUpdatedUtc = DateTime.UtcNow,
                LastUpdatedByUserId = dto.UpdatedByUserId,
                LastUpdatedByUsername = dto.UpdatedByUsername
            };
            
            await _repository.UpsertAsync(entity);
            
            LogInfo("Configuration {Key} set to {Value} ({DisplayName}) by {Username}",
                dto.Key, dto.ValueId, dto.DisplayName, dto.UpdatedByUsername);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to set configuration: {Key}", dto.Key);
            throw;
        }
    }
    
    // Set a role configuration
    public async Task SetRoleAsync(SetConfigDto dto)
    {
        try
        {
            var entity = new ConfigurationEntity
            {
                Key = dto.Key.ToString(),
                Value = dto.ValueId.ToString(),
                DisplayName = dto.DisplayName,
                LastUpdatedUtc = DateTime.UtcNow,
                LastUpdatedByUserId = dto.UpdatedByUserId,
                LastUpdatedByUsername = dto.UpdatedByUsername
            };
            
            await _repository.UpsertAsync(entity);
            
            LogInfo("Configuration {Key} set to {Value} ({DisplayName}) by {Username}",
                dto.Key, dto.ValueId, dto.DisplayName, dto.UpdatedByUsername);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to set configuration: {Key}", dto.Key);
            throw;
        }
    }
    
    // Set a category configuration
    public async Task SetCategoryAsync(SetConfigDto dto)
    {
        try
        {
            var entity = new ConfigurationEntity
            {
                Key = dto.Key.ToString(),
                Value = dto.ValueId.ToString(),
                DisplayName = dto.DisplayName,
                LastUpdatedUtc = DateTime.UtcNow,
                LastUpdatedByUserId = dto.UpdatedByUserId,
                LastUpdatedByUsername = dto.UpdatedByUsername
            };
            
            await _repository.UpsertAsync(entity);
            
            LogInfo("Configuration {Key} set to {Value} ({DisplayName}) by {Username}",
                dto.Key, dto.ValueId, dto.DisplayName, dto.UpdatedByUsername);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to set configuration: {Key}", dto.Key);
            throw;
        }
    }
    
    // Generic set method (can be used for any config type)
    public async Task SetConfigAsync(ConfigKey key, ulong valueId, string displayName, ulong updatedByUserId, string updatedByUsername)
    {
        try
        {
            var entity = new ConfigurationEntity
            {
                Key = key.ToString(),
                Value = valueId.ToString(),
                DisplayName = displayName,
                LastUpdatedUtc = DateTime.UtcNow,
                LastUpdatedByUserId = updatedByUserId,
                LastUpdatedByUsername = updatedByUsername
            };
            
            await _repository.UpsertAsync(entity);
            
            LogInfo("Configuration {Key} set to {Value} ({DisplayName}) by {Username}",
                key, valueId, displayName, updatedByUsername);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to set configuration: {Key}", key);
            throw;
        }
    }
    
    // Delete a configuration
    public async Task DeleteConfigAsync(ConfigKey key)
    {
        try
        {
            await _repository.DeleteAsync(key.ToString());
            
            LogInfo("Configuration {Key} deleted", key);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to delete configuration: {Key}", key);
            throw;
        }
    }
    
    #endregion
}