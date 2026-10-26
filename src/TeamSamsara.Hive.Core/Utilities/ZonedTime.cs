namespace TeamSamsara.Hive.Core.Utilities;

// Time zone handling for anything people schedule ("Monday 8 PM").
//
// Rules used across the bot:
//   - Times are STORED in UTC.
//   - Times people TYPE are read as wall-clock time in the configured zone
//     (e.g. America/Toronto), so "8 PM" stays 8 PM across daylight saving.
//   - Times the bot SHOWS use Discord timestamps (<t:...>), which every
//     viewer sees in their own local time.
public static class ZonedTime
{
    public const string DefaultTimeZoneId = "America/Toronto";

    // Looks up an IANA time zone id ("America/Toronto", "Europe/Paris").
    // Works on Linux (Render) and on Windows with .NET 6+.
    public static bool TryResolve(string? timeZoneId, out TimeZoneInfo timeZone)
    {
        timeZone = TimeZoneInfo.Utc;
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return false;
        }

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    public static TimeZoneInfo Resolve(string? timeZoneId) =>
        TryResolve(timeZoneId, out var timeZone)
            ? timeZone
            : throw new ArgumentException($"Unknown time zone '{timeZoneId}'. Use an IANA name such as {DefaultTimeZoneId}.", nameof(timeZoneId));

    // Converts a wall-clock time in the given zone to UTC.
    //   - A time skipped by "spring forward" (e.g. 2:30 AM that night) moves
    //     forward by the size of the jump, like a clock would show it.
    //   - A time that happens twice on "fall back" (e.g. 1:30 AM) takes the
    //     first occurrence (still on daylight time).
    public static DateTime ToUtc(DateTime wallClock, TimeZoneInfo timeZone)
    {
        var local = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);

        if (timeZone.IsInvalidTime(local))
        {
            var gap = GetDaylightDelta(timeZone, local);
            local = local.Add(gap);
        }

        if (timeZone.IsAmbiguousTime(local))
        {
            var offsets = timeZone.GetAmbiguousTimeOffsets(local);
            var daylightOffset = offsets.Max(); // the earlier instant
            return DateTime.SpecifyKind(local - daylightOffset, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }

    // Converts a UTC instant to wall-clock time in the given zone
    public static DateTime ToLocal(DateTime utc, TimeZoneInfo timeZone)
    {
        var asUtc = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(asUtc, timeZone);
    }

    // Discord timestamp markup, shown in each viewer's own time zone.
    // Styles: t = 4:30 PM, T = 4:30:00 PM, d = 12/14/2026, D = December 14, 2026,
    //         f = December 14, 2026 4:30 PM (default), F = Monday, December 14, 2026 4:30 PM,
    //         R = "in 2 hours" / "3 days ago".
    // Only renders in message text and embed descriptions/fields - not in
    // embed titles, footers, author names or button labels.
    public static string DiscordTimestamp(DateTime utc, char style = 'f')
    {
        if ("tTdDfFR".IndexOf(style) < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(style), style, "Discord timestamp styles are t, T, d, D, f, F and R.");
        }

        var asUtc = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        var unix = new DateTimeOffset(asUtc).ToUnixTimeSeconds();
        return $"<t:{unix}:{style}>";
    }

    // Size of the daylight-saving jump in effect around a given local time
    // (one hour in North America and Europe).
    private static TimeSpan GetDaylightDelta(TimeZoneInfo timeZone, DateTime local)
    {
        var rule = timeZone.GetAdjustmentRules()
            .FirstOrDefault(r => r.DateStart <= local.Date && r.DateEnd >= local.Date);
        return rule?.DaylightDelta is { } delta && delta > TimeSpan.Zero ? delta : TimeSpan.FromHours(1);
    }
}
