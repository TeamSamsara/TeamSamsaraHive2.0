using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using TeamSamsara.Hive.Bot.Integrations.Twitch;
using TeamSamsara.Hive.Bot.Streams.Models.Entities;
using TeamSamsara.Hive.Bot.Streams.Repositories;
using TeamSamsara.Hive.Core.Abstractions;

namespace TeamSamsara.Hive.Bot.Streams.Services;

public class AddStreamResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

// Handles /stream add - validates the Twitch user exists, then persists it
// so it only needs to be added once
public class AddStreamService : BaseService
{
    private readonly IStreamRepository _repository;
    private readonly TwitchIntegration _twitch;

    public override string ServiceName => "AddStream";

    public AddStreamService(
        IStreamRepository repository,
        TwitchIntegration twitch,
        ILogger<AddStreamService> logger,
        IConfiguration configuration)
        : base(logger, configuration)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _twitch = twitch ?? throw new ArgumentNullException(nameof(twitch));
    }

    public async Task<AddStreamResultDto> AddStreamAsync(
        string usernameOrLink,
        ulong addedByUserId,
        string addedByUsername,
        ulong? notificationRoleId = null)
    {
        var username = ExtractUsername(usernameOrLink);

        if (string.IsNullOrWhiteSpace(username))
        {
            return new AddStreamResultDto { Success = false, Message = "That doesn't look like a valid Twitch username or link." };
        }

        try
        {
            if (await _repository.ExistsAsync(username))
            {
                return new AddStreamResultDto { Success = false, Message = $"**{username}** is already being tracked." };
            }

            var user = await _twitch.GetUserInfoAsync(username);
            if (user == null)
            {
                return new AddStreamResultDto { Success = false, Message = $"Couldn't find a Twitch user named **{username}**." };
            }

            var entity = new TrackedStreamEntity
            {
                TwitchUsername = user.login,
                DisplayName = user.display_name,
                AddedByUserId = addedByUserId,
                AddedByUsername = addedByUsername,
                AddedAtUtc = DateTime.UtcNow,
                IsLive = false,
                NotificationRoleId = notificationRoleId
            };

            await _repository.AddAsync(entity);

            LogInfo("Stream added: {Username} by {AddedBy} (role: {RoleId})", user.login, addedByUsername, notificationRoleId);

            var roleNote = notificationRoleId.HasValue
                ? " That role will be pinged instead of the default one for this streamer."
                : string.Empty;

            return new AddStreamResultDto
            {
                Success = true,
                Message = $"✅ Now tracking **{user.display_name}**. You'll get a notification when they go live.{roleNote}"
            };
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to add stream: {Input}", usernameOrLink);
            return new AddStreamResultDto { Success = false, Message = "Something went wrong adding that stream. Check the logs." };
        }
    }

    // Sets or clears (roleId = null) the per-stream notification role override
    // for an already-tracked streamer, via /stream set-role
    public async Task<AddStreamResultDto> SetStreamRoleAsync(string usernameOrLink, ulong? roleId)
    {
        var username = ExtractUsername(usernameOrLink);

        if (string.IsNullOrWhiteSpace(username))
        {
            return new AddStreamResultDto { Success = false, Message = "That doesn't look like a valid Twitch username or link." };
        }

        try
        {
            var entity = await _repository.GetByUsernameAsync(username);
            if (entity == null)
            {
                return new AddStreamResultDto { Success = false, Message = $"**{username}** isn't being tracked yet - use `/stream add` first." };
            }

            entity.NotificationRoleId = roleId;
            await _repository.UpdateAsync(entity);

            LogInfo("Notification role for {Username} set to {RoleId}", username, roleId);

            var message = roleId.HasValue
                ? $"✅ **{entity.DisplayName}** will now ping <@&{roleId.Value}> when live."
                : $"✅ **{entity.DisplayName}** will use the default notification role again.";

            return new AddStreamResultDto { Success = true, Message = message };
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to set notification role for: {Input}", usernameOrLink);
            return new AddStreamResultDto { Success = false, Message = "Something went wrong updating that streamer's role. Check the logs." };
        }
    }

    // Accepts a bare username ("shroud") or a full link ("https://twitch.tv/shroud")
    // and returns the plain lowercase username
    private string ExtractUsername(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        input = input.Trim();

        var match = Regex.Match(input, @"twitch\.tv/([a-zA-Z0-9_]+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.ToLowerInvariant();
        }

        // Assume bare username - Twitch usernames are 4-25 chars, letters/numbers/underscore
        return Regex.IsMatch(input, @"^[a-zA-Z0-9_]{4,25}$") ? input.ToLowerInvariant() : string.Empty;
    }
}