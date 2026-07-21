using TeamSamsara.Hive.Bot.Streams.Models.Entities;
using TeamSamsara.Hive.Core.Interfaces;

namespace TeamSamsara.Hive.Bot.Streams.Repositories;

// Interface defining repository contract for tracked-stream data access
public interface IStreamRepository : IRepository
{
    Task<TrackedStreamEntity?> GetByUsernameAsync(string twitchUsername);
    Task<List<TrackedStreamEntity>> GetAllAsync();
    Task<TrackedStreamEntity> AddAsync(TrackedStreamEntity entity);
    Task UpdateAsync(TrackedStreamEntity entity);
    Task<bool> RemoveAsync(string twitchUsername);
    Task<bool> ExistsAsync(string twitchUsername);
}