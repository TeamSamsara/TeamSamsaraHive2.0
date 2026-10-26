using TeamSamsara.Hive.Core.Utilities;

namespace TeamSamsara.Hive.Bot.Protocols.Models;

// Server-wide settings for the protocols feature, changed with /protocol setup
// and /protocol role. One record for the whole server.
public class ProtocolSettings
{
    // The Discord category that holds every protocol channel
    public ulong? CategoryId { get; set; }

    // Where the pinned calendar lives
    public ulong? CalendarChannelId { get; set; }

    // Where the Monday digest and planning checks are posted
    public ulong? DigestChannelId { get; set; }

    // Who gets the "plan the next weeks" reminders
    public ulong? PlannerRoleId { get; set; }

    // IANA zone that typed times are read in ("8 PM" = 8 PM here).
    // Displayed times use Discord timestamps, so viewers see their own time.
    public string TimeZoneId { get; set; } = ZonedTime.DefaultTimeZoneId;

    // Owner names used in templates ("Design", "Social") mapped to Discord
    // roles. Templates refer to the name, so a role can be swapped without
    // editing any template.
    public List<OwnerRole> OwnerRoles { get; set; } = new();

    public DateTime? UpdatedAtUtc { get; set; }
    public ulong? UpdatedByUserId { get; set; }
    public string? UpdatedByUsername { get; set; }

    public OwnerRole? FindOwner(string name) =>
        OwnerRoles.FirstOrDefault(o => string.Equals(o.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    public ProtocolSettings Copy() => new()
    {
        CategoryId = CategoryId,
        CalendarChannelId = CalendarChannelId,
        DigestChannelId = DigestChannelId,
        PlannerRoleId = PlannerRoleId,
        TimeZoneId = TimeZoneId,
        OwnerRoles = OwnerRoles.Select(o => new OwnerRole { Name = o.Name, RoleId = o.RoleId }).ToList(),
        UpdatedAtUtc = UpdatedAtUtc,
        UpdatedByUserId = UpdatedByUserId,
        UpdatedByUsername = UpdatedByUsername
    };
}

public class OwnerRole
{
    public string Name { get; set; } = string.Empty;
    public ulong RoleId { get; set; }
}
