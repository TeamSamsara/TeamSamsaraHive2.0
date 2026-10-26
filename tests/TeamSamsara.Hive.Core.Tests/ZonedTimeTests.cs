using TeamSamsara.Hive.Core.Utilities;
using Xunit;

namespace TeamSamsara.Hive.Core.Tests;

// Times people type are read in the server's zone; these pin down the
// tricky cases (daylight saving changes) before planning is built on them.
public class ZonedTimeTests
{
    private static readonly TimeZoneInfo Toronto = ZonedTime.Resolve("America/Toronto");

    [Fact]
    public void Resolves_iana_names_and_rejects_unknown_ones()
    {
        Assert.True(ZonedTime.TryResolve("America/Toronto", out _));
        Assert.True(ZonedTime.TryResolve("Europe/Paris", out _));
        Assert.False(ZonedTime.TryResolve("Mars/Olympus_Mons", out _));
        Assert.False(ZonedTime.TryResolve("", out _));
        Assert.False(ZonedTime.TryResolve(null, out _));
        Assert.Throws<ArgumentException>(() => ZonedTime.Resolve("Not/AZone"));
    }

    [Fact]
    public void Winter_evening_in_toronto_is_utc_minus_5()
    {
        // Mon Dec 14 2026, 7:00 PM EST
        var utc = ZonedTime.ToUtc(new DateTime(2026, 12, 14, 19, 0, 0), Toronto);

        Assert.Equal(new DateTime(2026, 12, 15, 0, 0, 0, DateTimeKind.Utc), utc);
        Assert.Equal(DateTimeKind.Utc, utc.Kind);
    }

    [Fact]
    public void Summer_evening_in_toronto_is_utc_minus_4()
    {
        // Thu Jul 16 2026, 7:00 PM EDT
        var utc = ZonedTime.ToUtc(new DateTime(2026, 7, 16, 19, 0, 0), Toronto);

        Assert.Equal(new DateTime(2026, 7, 16, 23, 0, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void Same_wall_clock_time_keeps_its_meaning_across_the_daylight_change()
    {
        // "Every Monday at 8 PM" stays 8 PM local even though the UTC hour changes
        var beforeChange = ZonedTime.ToUtc(new DateTime(2026, 10, 26, 20, 0, 0), Toronto); // EDT
        var afterChange = ZonedTime.ToUtc(new DateTime(2026, 11, 2, 20, 0, 0), Toronto);   // EST

        Assert.Equal(20, ZonedTime.ToLocal(beforeChange, Toronto).Hour);
        Assert.Equal(20, ZonedTime.ToLocal(afterChange, Toronto).Hour);
        Assert.Equal(0, beforeChange.Hour); // 8 PM EDT = 00:00 UTC
        Assert.Equal(1, afterChange.Hour);  // 8 PM EST = 01:00 UTC
    }

    [Fact]
    public void Time_skipped_by_spring_forward_moves_ahead_by_an_hour()
    {
        // Sun Mar 8 2026: 2:00 AM jumps to 3:00 AM, so 2:30 AM never exists
        var utc = ZonedTime.ToUtc(new DateTime(2026, 3, 8, 2, 30, 0), Toronto);

        Assert.Equal(new DateTime(2026, 3, 8, 7, 30, 0, DateTimeKind.Utc), utc); // 3:30 AM EDT
    }

    [Fact]
    public void Time_repeated_by_fall_back_uses_the_first_occurrence()
    {
        // Sun Nov 1 2026: 1:30 AM happens twice; take the earlier one (EDT)
        var utc = ZonedTime.ToUtc(new DateTime(2026, 11, 1, 1, 30, 0), Toronto);

        Assert.Equal(new DateTime(2026, 11, 1, 5, 30, 0, DateTimeKind.Utc), utc);
    }

    [Fact]
    public void Discord_timestamps_use_unix_seconds_and_style()
    {
        var utc = new DateTime(2026, 12, 15, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal("<t:1797292800:F>", ZonedTime.DiscordTimestamp(utc, 'F'));
        Assert.Equal("<t:1797292800:R>", ZonedTime.DiscordTimestamp(utc, 'R'));
        Assert.Equal("<t:1797292800:f>", ZonedTime.DiscordTimestamp(utc));
        Assert.Throws<ArgumentOutOfRangeException>(() => ZonedTime.DiscordTimestamp(utc, 'x'));
    }
}
