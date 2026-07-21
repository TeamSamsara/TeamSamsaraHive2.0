using TeamSamsara.Hive.Bot.Configuration.Models.Entities;
using TeamSamsara.Hive.Core.Interfaces;

namespace TeamSamsara.Hive.Bot.Configuration.Repositories;

// Interface defining repository contract for Configuration data access
// Implemented by ConfigurationRepository
public interface IConfigurationRepository : IRepository
{
    // Get a configuration by key
    Task<ConfigurationEntity?> GetByKeyAsync(string key);
    
    // Get all configurations
    Task<List<ConfigurationEntity>> GetAllAsync();
    
    // Insert or update a configuration (upsert)
    Task UpsertAsync(ConfigurationEntity entity);
    
    // Delete a configuration by key
    Task DeleteAsync(string key);
    
    // Check if a configuration key exists
    Task<bool> ExistsAsync(string key);
}