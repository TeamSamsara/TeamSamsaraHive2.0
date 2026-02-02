using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using TeamSamsara.Hive.Bot.Configuration.Models.DTOs;
using TeamSamsara.Hive.Bot.Configuration.Models.Enums;
using TeamSamsara.Hive.Bot.Configuration.Repositories;
using TeamSamsara.Hive.Bot.Integrations.Discord;

using TeamSamsara.Hive.Core.Abstractions;

using Discord;

namespace TeamSamsara.Hive.Bot.Configuration.Services;

// Service for validating that configured Discord IDs actually exist
// Used by /config validate command to check configuration health
public class ValidateConfigService : BaseService
{
    private readonly IConfigurationRepository _repository;
    private readonly DiscordIntegration _discord;
    
    public override string ServiceName => "ValidateConfig";

    #region Constructor
    
    public ValidateConfigService(
        IConfigurationRepository repository,
        DiscordIntegration discord,
        ILogger<ValidateConfigService> logger,
        IConfiguration configuration)
        : base(logger, configuration)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _discord = discord ?? throw new ArgumentNullException(nameof(discord));
    }
    
    #endregion

    #region Public API
    
    // Validate a single channel ID exists
    public async Task<bool> ValidateChannelExistsAsync(ulong channelId)
    {
        try
        {
            var channel = await _discord.GetChannelAsync(channelId);
            return channel != null;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to validate channel: {ChannelId}", channelId);
            return false;
        }
    }
    
    // Validate a single role ID exists
    public async Task<bool> ValidateRoleExistsAsync(ulong roleId)
    {
        try
        {
            var guild = _discord.Client?.Guilds.FirstOrDefault();
            if (guild == null)
            {
                LogWarning("Bot is not connected to any guild");
                return false;
            }
            
            var role = guild.GetRole(roleId);
            return role != null;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to validate role: {RoleId}", roleId);
            return false;
        }
    }
    
    // Validate a category ID exists
    public async Task<bool> ValidateCategoryExistsAsync(ulong categoryId)
    {
        try
        {
            var channel = await _discord.GetChannelAsync(categoryId);
            return channel != null && channel is ICategoryChannel;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to validate category: {CategoryId}", categoryId);
            return false;
        }
    }
    
    // Validate ALL configurations
    public async Task<ValidationResultDto> ValidateAllConfigurationsAsync()
    {
        var result = new ValidationResultDto
        {
            TotalConfigurations = 0,
            ValidConfigurations = 0
        };
        
        try
        {
            var allConfigs = await _repository.GetAllAsync();
            result.TotalConfigurations = allConfigs.Count;
            
            foreach (var config in allConfigs)
            {
                if (!ulong.TryParse(config.Value, out var id))
                {
                    result.InvalidConfigurations.Add(new InvalidConfigDto
                    {
                        Key = config.Key,
                        Reason = "Invalid ID format",
                        LastUpdated = config.LastUpdatedUtc,
                        LastUpdatedBy = config.LastUpdatedByUsername
                    });
                    continue;
                }
                
                bool isValid = false;
                
                // Determine type and validate
                if (IsChannelKey(config.Key))
                {
                    isValid = await ValidateChannelExistsAsync(id);
                }
                else if (IsRoleKey(config.Key))
                {
                    isValid = await ValidateRoleExistsAsync(id);
                }
                else if (IsCategoryKey(config.Key))
                {
                    isValid = await ValidateCategoryExistsAsync(id);
                }
                
                if (isValid)
                {
                    result.ValidConfigurations++;
                }
                else
                {
                    result.InvalidConfigurations.Add(new InvalidConfigDto
                    {
                        Key = config.Key,
                        Reason = "ID does not exist in Discord",
                        LastUpdated = config.LastUpdatedUtc,
                        LastUpdatedBy = config.LastUpdatedByUsername
                    });
                }
            }
            
            LogInfo("Validation complete: {Valid}/{Total} configurations valid",
                result.ValidConfigurations, result.TotalConfigurations);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to validate all configurations");
        }
        
        return result;
    }
    
    #endregion
    
    #region Private Methods
    
    // Check if key is a channel key
    private bool IsChannelKey(string key)
    {
        return key == ConfigKey.StreamNotificationChannelId.ToString() ||
               key == ConfigKey.UrgentTicketChannelId.ToString() ||
               key == ConfigKey.AdminChannelId.ToString() ||
               key == ConfigKey.AnalyticsReportChannelId.ToString() ||
               key == ConfigKey.TicketLogChannelId.ToString() ||
               key == ConfigKey.UrgentStatsChannelId.ToString() ||
               key == ConfigKey.HighStatsChannelId.ToString() ||
               key == ConfigKey.NormalStatsChannelId.ToString() ||
               key == ConfigKey.EscalatedStatsChannelId.ToString() ||
               key == ConfigKey.OldestStatsChannelId.ToString();
    }
    
    // Check if key is a role key
    private bool IsRoleKey(string key)
    {
        return key == ConfigKey.AdminRoleId.ToString() ||
               key == ConfigKey.ModeratorRoleId.ToString() ||
               key == ConfigKey.StaffRoleId.ToString() ||
               key == ConfigKey.StreamNotificationRoleId.ToString();
    }
    
    // Check if key is a category key
    private bool IsCategoryKey(string key)
    {
        return key == ConfigKey.ActiveTicketsCategoryId.ToString() ||
               key == ConfigKey.ClosedTicketsCategoryId.ToString();
    }
    
    #endregion
}