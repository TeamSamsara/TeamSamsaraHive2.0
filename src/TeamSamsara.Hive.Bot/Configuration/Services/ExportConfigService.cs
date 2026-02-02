using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using TeamSamsara.Hive.Bot.Configuration.Models.DTOs;
using TeamSamsara.Hive.Bot.Configuration.Repositories;

using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Configuration.Services;

// Service for exporting configuration to JSON file
// Used by /config export command
public class ExportConfigService : BaseService
{
    private readonly IConfigurationRepository _repository;
    
    public override string ServiceName => "ExportConfig";

    #region Constructor
    
    public ExportConfigService(
        IConfigurationRepository repository,
        ILogger<ExportConfigService> logger,
        IConfiguration configuration)
        : base(logger, configuration)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }
    
    #endregion

    #region Public API
    
    // Export all configurations to JSON string
    public async Task<string> ExportToJsonAsync(ulong exportedByUserId, string exportedByUsername)
    {
        try
        {
            var allConfigs = await _repository.GetAllAsync();
            
            var export = new ExportConfigDto
            {
                ExportDate = DateTime.UtcNow,
                ExportedBy = exportedByUsername,
                ExportedByUserId = exportedByUserId,
                Configurations = allConfigs.Select(c => new ConfigItemDto
                {
                    Key = c.Key,
                    Value = c.Value,
                    DisplayName = c.DisplayName,
                    LastUpdated = c.LastUpdatedUtc
                }).ToList()
            };
            
            var json = JsonSerializer.Serialize(export, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            LogInfo("Configuration exported by {Username} ({Count} entries)", 
                exportedByUsername, allConfigs.Count);
            
            return json;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to export configuration");
            throw;
        }
    }
    
    #endregion
}