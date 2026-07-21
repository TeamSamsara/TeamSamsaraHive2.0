using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TeamSamsara.Hive.Bot.Streams.Data;
using TeamSamsara.Hive.Bot.Streams.Models.Entities;
using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Streams.Repositories;

public class StreamRepository : BaseRepository, IStreamRepository
{
    private readonly StreamDbContext _context;

    public override string RepositoryName => "StreamRepository";

    public StreamRepository(StreamDbContext context, ILogger<StreamRepository> logger) : base(logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<TrackedStreamEntity?> GetByUsernameAsync(string twitchUsername)
    {
        try
        {
            return await _context.TrackedStreams
                .FirstOrDefaultAsync(s => s.TwitchUsername == twitchUsername.ToLowerInvariant());
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get stream by username: {Username}", twitchUsername);
            throw;
        }
    }

    public async Task<List<TrackedStreamEntity>> GetAllAsync()
    {
        try
        {
            return await _context.TrackedStreams.ToListAsync();
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to get all tracked streams");
            throw;
        }
    }

    public async Task<TrackedStreamEntity> AddAsync(TrackedStreamEntity entity)
    {
        try
        {
            entity.TwitchUsername = entity.TwitchUsername.ToLowerInvariant();
            await _context.TrackedStreams.AddAsync(entity);
            await _context.SaveChangesAsync();

            LogInfo("Stream added: {Username}", entity.TwitchUsername);
            return entity;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to add stream: {Username}", entity.TwitchUsername);
            throw;
        }
    }

    public async Task UpdateAsync(TrackedStreamEntity entity)
    {
        try
        {
            _context.TrackedStreams.Update(entity);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to update stream: {Username}", entity.TwitchUsername);
            throw;
        }
    }

    public async Task<bool> RemoveAsync(string twitchUsername)
    {
        try
        {
            var entity = await GetByUsernameAsync(twitchUsername);
            if (entity == null) return false;

            _context.TrackedStreams.Remove(entity);
            await _context.SaveChangesAsync();

            LogInfo("Stream removed: {Username}", twitchUsername);
            return true;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to remove stream: {Username}", twitchUsername);
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string twitchUsername)
    {
        try
        {
            return await _context.TrackedStreams
                .AnyAsync(s => s.TwitchUsername == twitchUsername.ToLowerInvariant());
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to check if stream exists: {Username}", twitchUsername);
            throw;
        }
    }
}