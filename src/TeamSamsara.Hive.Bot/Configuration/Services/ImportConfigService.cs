using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using TeamSamsara.Hive.Bot.Configuration.Models.DTOs;
using TeamSamsara.Hive.Bot.Configuration.Models.Entities;
using TeamSamsara.Hive.Bot.Configuration.Repositories;

using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Configuration.Services;

// Service for importing configuration from JSON file
// Used by /config import command
public class ImportConfigService : BaseService
{
    private readonly IConfigurationRepository _repository;
    
    public override string ServiceName => "ImportConfig";

    #region Constructor
    
    public ImportConfigService(
        IConfigurationRepository repository,
        ILogger<ImportConfigService> logger,
        IConfiguration configuration)
        : base(logger, configuration)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }
    
    #endregion

    #region Public API
    
    // Import configurations from JSON string
    public async Task<(int imported, int failed, List<string> errors)> ImportFromJsonAsync(
        string jsonContent,
        ulong importedByUserId,
        string importedByUsername)
    {
        int imported = 0;
        int failed = 0;
        var errors = new List<string>();
        
        try
        {
            var import = JsonSerializer.Deserialize<ImportConfigDto>(jsonContent);
            
            if (import == null || import.Configurations == null)
            {
                throw new InvalidOperationException("Invalid JSON format");
            }
            
            LogInfo("Importing {Count} configurations requested by {Username}", 
                import.Configurations.Count, importedByUsername);
            
            foreach (var configItem in import.Configurations)
            {
                try
                {
                    // Validate ID format
                    if (!ulong.TryParse(configItem.Value, out var id))
                    {
                        errors.Add($"{configItem.Key}: Invalid ID format");
                        failed++;
                        continue;
                    }
                    
                    // Create entity
                    var entity = new ConfigurationEntity
                    {
                        Key = configItem.Key,
                        Value = configItem.Value,
                        DisplayName = configItem.DisplayName,
                        LastUpdatedUtc = DateTime.UtcNow,
                        LastUpdatedByUserId = importedByUserId,
                        LastUpdatedByUsername = importedByUsername
                    };
                    
                    // Save to database
                    await _repository.UpsertAsync(entity);
                    imported++;
                    
                    LogDebug("Imported configuration: {Key}", configItem.Key);
                }
                catch (Exception ex)
                {
                    errors.Add($"{configItem.Key}: {ex.Message}");
                    failed++;
                    LogWarning("Failed to import {Key}: {Error}", configItem.Key, ex.Message);
                }
            }
            
            LogInfo("Import complete: {Imported} imported, {Failed} failed", imported, failed);
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to import configuration");
            throw;
        }
        
        return (imported, failed, errors);
    }
    
    #endregion
}