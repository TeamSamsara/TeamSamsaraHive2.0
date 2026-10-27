using TeamSamsara.Hive.Core.Scheduling;
using TeamSamsara.Hive.Core.Utilities;
using Xunit;

namespace TeamSamsara.Hive.Core.Tests;

// What people type for "when is this step due", relative to an event that
// starts Mon Dec 14 2026 at 7:00 PM Toronto time.
public class DueOffsetTests
{
    private static readonly TimeZoneInfo Toronto = ZonedTime.Resolve("America/Toronto");
    private static readonly DateTime EventStartUtc = ZonedTime.ToUtc(new DateTime(2026, 12, 14, 19, 0, 0), Toronto);

    [Theory]
    [InlineData("2 hours before", "-2h", "2 hours before", "2026-12-14 17:00")]
    [InlineData("1h before", "-1h", "1 hour before", "2026-12-14 18:00")]
    [InlineData("30 min after", "+30m", "30 min after", "2026-12-14 19:30")]
    [InlineData("1 day 2 hours before", "-1d2h", "1 day 2 hours before", "2026-12-13 17:00")]
    [InlineData("at start", "0", "at start", "2026-12-14 19:00")]
    [InlineData("day of 12:00", "0d@12:00", "day of at 12:00 PM", "2026-12-14 12:00")]
    [InlineData("day of at 9 am", "0d@09:00", "day of at 9:00 AM", "2026-12-14 09:00")]
    [InlineData("the day before at 18:00", "-1d@18:00", "day before at 6:00 PM", "2026-12-13 18:00")]
    [InlineData("day after 10:00", "+1d@10:00", "day after at 10:00 AM", "2026-12-15 10:00")]
    [InlineData("5 days before 12 PM", "-5d@12:00", "5 days before at 12:00 PM", "2026-12-09 12:00")]
    [InlineData("2 days before noon", "-2d@12:00", "2 days before at 12:00 PM", "2026-12-12 12:00")]
    [InlineData("12:00", "0d@12:00", "day of at 12:00 PM", "2026-12-14 12:00")]
    [InlineData("-2d@09:00", "-2d@09:00", "2 days before at 9:00 AM", "2026-12-12 09:00")]
    [InlineData("+1d", "+1d", "1 day after", "2026-12-15 19:00")]
    [InlineData("3 days before", "-3d", "3 days before", "2026-12-11 19:00")]
    public void Parses_describes_and_resolves(string input, string spec, string description, string dueLocal)
    {
        Assert.True(DueOffset.TryParse(input, out var offset, out var error), error);

        Assert.Equal(spec, offset.ToSpec());
        Assert.Equal(description, offset.Describe());
        Assert.Equal(dueLocal, ZonedTime.ToLocal(offset.ResolveUtc(EventStartUtc, Toronto), Toronto).ToString("yyyy-MM-dd HH:mm"));

        // The stored spec always reads back as the same offset
        Assert.Equal(offset, DueOffset.Parse(offset.ToSpec()));
    }

    [Theory]
    [InlineData("2 hours")]               // before or after?
    [InlineData("day of")]                // which time?
    [InlineData("2 hours before 12:00")]  // can't mix hours with a clock time
    [InlineData("whenever")]
    [InlineData("")]
    [InlineData("25:00")]
    [InlineData("13 pm")]
    public void Rejects_unclear_input_with_a_message(string input)
    {
        Assert.False(DueOffset.TryParse(input, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    // Edit forms are prefilled with Describe(), so it must read back unchanged
    [Theory]
    [InlineData("-2h")]
    [InlineData("+30m")]
    [InlineData("-90m")]
    [InlineData("+2h30m")]
    [InlineData("-1d2h30m")]
    [InlineData("0")]
    [InlineData("0d@09:00")]
    [InlineData("-1d@00:00")]
    [InlineData("-2d@23:59")]
    [InlineData("+1d@10:00")]
    [InlineData("-3d")]
    public void Description_reads_back_as_the_same_offset(string spec)
    {
        var offset = DueOffset.Parse(spec);

        Assert.True(DueOffset.TryParse(offset.Describe(), out var back, out var error), error);
        Assert.Equal(offset, back);
    }

    [Fact]
    public void Day_and_clock_time_stay_local_across_daylight_saving()
    {
        // Event Mon Nov 2 2026 7 PM (EST); "2 days before 12:00" is Sat Oct 31 12:00 (EDT)
        var start = ZonedTime.ToUtc(new DateTime(2026, 11, 2, 19, 0, 0), Toronto);

        var due = DueOffset.Parse("2 days before 12:00").ResolveUtc(start, Toronto);

        Assert.Equal(new DateTime(2026, 10, 31, 12, 0, 0), ZonedTime.ToLocal(due, Toronto));
    }
}

public class TemplateTextTests
{
    [Fact]
    public void Fills_known_values_and_reports_the_rest()
    {
        var values = new Dictionary<string, string?> { ["opponent"] = "Northwind", ["time"] = "7:00 PM", ["unused"] = "x" };

        var text = TemplateText.Fill("[CAPTION]\nTonight vs {opponent} at {time}\n{stream_link}", values, out var missing);

        Assert.Equal("[CAPTION]\nTonight vs Northwind at 7:00 PM\n{stream_link}", text);
        Assert.Equal(new[] { "stream_link" }, missing);
    }

    [Fact]
    public void Finds_placeholders_once_in_order()
    {
        Assert.Equal(new[] { "a", "b_2" }, TemplateText.FindPlaceholders("{a} {b_2} {a} {Bad} {}"));
    }

    [Theory]
    [InlineData("Stream Link", "stream_link")]
    [InlineData("Opponent", "opponent")]
    [InlineData("2nd map", "f_2nd_map")]
    [InlineData("!!", null)]
    public void Turns_labels_into_keys(string label, string? key)
    {
        Assert.Equal(key, TemplateText.ToKey(label));
    }
}
