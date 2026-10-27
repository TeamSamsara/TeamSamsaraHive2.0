using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TeamSamsara.Hive.Core.Utilities;

namespace TeamSamsara.Hive.Core.Scheduling;

// "When" a step is due (or a thread opens), relative to an event's start.
//
// Two shapes:
//   - a duration:        "2 hours before", "30 min after", "1 day 2 hours before", "at start"
//   - a day + clock time: "day of 12:00", "2 days before at 9 AM", "day after 10:00"
//
// People type the friendly form; it is stored as a short spec that parses
// back to the same thing:
//   "-2h", "+30m", "-1d2h", "0", "0d@12:00", "-2d@09:00", "+1d@10:00"
//
// Days are counted on the calendar in the server's time zone (so "1 day
// before at 9 AM" is 9 AM local the day before, even across daylight saving).
// Hours and minutes are real elapsed time.
public sealed class DueOffset : IEquatable<DueOffset>
{
    public static readonly DueOffset AtStart = new(0, 0, 0, null);

    // Negative = before the event, positive = after
    public int Days { get; }
    public int Hours { get; }
    public int Minutes { get; }

    // When set, the step is due at this local clock time on (event day + Days);
    // Hours and Minutes are then always 0
    public TimeOnly? AtTime { get; }

    private DueOffset(int days, int hours, int minutes, TimeOnly? atTime)
    {
        Days = days;
        Hours = hours;
        Minutes = minutes;
        AtTime = atTime;
    }

    public static DueOffset Duration(int days, int hours, int minutes)
    {
        var sign = Math.Sign(days) != 0 ? Math.Sign(days) : Math.Sign(hours) != 0 ? Math.Sign(hours) : Math.Sign(minutes);
        if ((days != 0 && Math.Sign(days) != sign) || (hours != 0 && Math.Sign(hours) != sign) || (minutes != 0 && Math.Sign(minutes) != sign))
        {
            throw new ArgumentException("All parts of a duration must point the same way (all before or all after).");
        }

        return new DueOffset(days, hours, minutes, null);
    }

    public static DueOffset DayAt(int days, TimeOnly atTime) => new(days, 0, 0, atTime);

    // When the step is due for an event starting at eventStartUtc
    public DateTime ResolveUtc(DateTime eventStartUtc, TimeZoneInfo timeZone)
    {
        var localStart = ZonedTime.ToLocal(eventStartUtc, timeZone);

        if (AtTime is { } at)
        {
            var localDue = localStart.Date.AddDays(Days).Add(at.ToTimeSpan());
            return ZonedTime.ToUtc(localDue, timeZone);
        }

        // Calendar days in local time, then real hours/minutes
        var dayShifted = ZonedTime.ToUtc(localStart.AddDays(Days), timeZone);
        return dayShifted.AddHours(Hours).AddMinutes(Minutes);
    }

    #region Spec (storage form)

    private static readonly Regex SpecPattern = new(
        @"^(?<sign>[+-])?(?:(?<d>\d+)d)?(?:(?<h>\d+)h)?(?:(?<m>\d+)m)?(?:@(?<th>\d{1,2}):(?<tm>\d{2}))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Short storage form: "-2h", "+1d@10:00", "0d@09:00", "0"
    public string ToSpec()
    {
        if (AtTime is { } at)
        {
            var sign = Days < 0 ? "-" : Days > 0 ? "+" : "";
            return $"{sign}{Math.Abs(Days)}d@{at.Hour:00}:{at.Minute:00}";
        }

        if (Days == 0 && Hours == 0 && Minutes == 0)
        {
            return "0";
        }

        var sb = new StringBuilder(Days < 0 || Hours < 0 || Minutes < 0 ? "-" : "+");
        if (Days != 0) sb.Append(Math.Abs(Days)).Append('d');
        if (Hours != 0) sb.Append(Math.Abs(Hours)).Append('h');
        if (Minutes != 0) sb.Append(Math.Abs(Minutes)).Append('m');
        return sb.ToString();
    }

    private static bool TryParseSpec(string text, out DueOffset? offset)
    {
        offset = null;
        if (text == "0")
        {
            offset = AtStart;
            return true;
        }

        var m = SpecPattern.Match(text);
        if (!m.Success || !(m.Groups["d"].Success || m.Groups["h"].Success || m.Groups["m"].Success || m.Groups["th"].Success))
        {
            return false;
        }

        var sign = m.Groups["sign"].Value == "-" ? -1 : 1;
        var days = m.Groups["d"].Success ? int.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture) * sign : 0;

        if (m.Groups["th"].Success)
        {
            if (m.Groups["h"].Success || m.Groups["m"].Success) return false;
            if (!TryMakeTime(int.Parse(m.Groups["th"].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups["tm"].Value, CultureInfo.InvariantCulture), out var time)) return false;
            offset = DayAt(days, time);
            return true;
        }

        if (m.Groups["sign"].Value == "" && (days != 0 || m.Groups["h"].Success || m.Groups["m"].Success))
        {
            return false; // a bare "2h" is ambiguous; the friendly parser asks for before/after
        }

        var hours = m.Groups["h"].Success ? int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture) * sign : 0;
        var minutes = m.Groups["m"].Success ? int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture) * sign : 0;
        offset = Duration(days, hours, minutes);
        return true;
    }

    #endregion

    #region Friendly parsing

    private static readonly Regex QuantityPattern = new(
        @"(?<n>\d+)\s*(?<u>days?|d|hours?|hrs?|h|minutes?|mins?|m)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TimePattern = new(
        @"(?:\bat\s+)?(?<h>\d{1,2})(?::(?<m>\d{2}))?\s*(?<ampm>am|pm)\b|(?:\bat\s+)?(?<h24>\d{1,2}):(?<m24>\d{2})\b|\b(?<word>noon|midnight)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Accepts the friendly forms and the spec form. Returns false with a
    // message saying what to type instead.
    public static bool TryParse(string? input, out DueOffset offset, out string error)
    {
        offset = AtStart;
        error = string.Empty;

        var text = (input ?? string.Empty).Trim().ToLowerInvariant()
            .Replace("a.m.", "am").Replace("p.m.", "pm");
        text = Regex.Replace(text, @"\s+", " ");

        if (text.Length == 0)
        {
            error = "Say when, e.g. \"2 hours before\", \"day of 12:00\" or \"at start\".";
            return false;
        }

        if (text is "start" or "at start" or "event start" or "on time" or "when it starts")
        {
            return true;
        }

        if (TryParseSpec(text.Replace(" ", ""), out var spec))
        {
            offset = spec!;
            return true;
        }

        // Clock time, if any ("12:00", "9 am", "noon")
        TimeOnly? time = null;
        var timeMatch = TimePattern.Match(text);
        if (timeMatch.Success)
        {
            if (!TryReadTime(timeMatch, out var t))
            {
                error = $"\"{timeMatch.Value.Trim()}\" isn't a valid time of day.";
                return false;
            }

            time = t;
            text = (text[..timeMatch.Index] + " " + text[(timeMatch.Index + timeMatch.Length)..]).Trim();
        }

        var before = Regex.IsMatch(text, @"\bbefore\b|\bearlier\b|\bprior\b");
        var after = Regex.IsMatch(text, @"\bafter\b|\blater\b");

        // Day words: "day of", "same day", "day before", "day after", "next day"
        int? dayWord = null;
        if (Regex.IsMatch(text, @"\b(day of|same day|that day|on the day)\b")) dayWord = 0;
        else if (Regex.IsMatch(text, @"^(the )?day before\b")) dayWord = -1;
        else if (Regex.IsMatch(text, @"^(the )?(day after|next day)\b")) dayWord = 1;

        int days = 0, hours = 0, minutes = 0;
        var anyQuantity = false;
        foreach (Match q in QuantityPattern.Matches(text))
        {
            anyQuantity = true;
            var n = int.Parse(q.Groups["n"].Value, CultureInfo.InvariantCulture);
            var unit = q.Groups["u"].Value;
            if (unit.StartsWith('d')) days += n;
            else if (unit.StartsWith('h')) hours += n;
            else minutes += n;
        }

        if (dayWord != null && !anyQuantity)
        {
            if (time == null)
            {
                error = "Add a time for that day, e.g. \"day of 12:00\" or \"day before 6 PM\".";
                return false;
            }

            offset = DayAt(dayWord.Value, time.Value);
            return true;
        }

        if (!anyQuantity)
        {
            if (time != null && Regex.IsMatch(text, @"^(at|on)?$"))
            {
                offset = DayAt(0, time.Value); // just "12:00" = that time on the day
                return true;
            }

            error = "I couldn't read that. Try \"2 hours before\", \"30 min after\", \"day of 12:00\" or \"2 days before 9 AM\".";
            return false;
        }

        if (before == after)
        {
            error = "Say whether it's before or after the start, e.g. \"2 hours before\".";
            return false;
        }

        var sign = before ? -1 : 1;

        if (time != null)
        {
            if (hours != 0 || minutes != 0)
            {
                error = "With a time of day, only use days, e.g. \"2 days before 12:00\".";
                return false;
            }

            offset = DayAt(sign * days, time.Value);
            return true;
        }

        offset = Duration(sign * days, sign * hours, sign * minutes);
        return true;
    }

    public static DueOffset Parse(string input) =>
        TryParse(input, out var offset, out var error) ? offset : throw new FormatException(error);

    private static bool TryReadTime(Match m, out TimeOnly time)
    {
        time = default;
        if (m.Groups["word"].Success)
        {
            time = m.Groups["word"].Value == "noon" ? new TimeOnly(12, 0) : new TimeOnly(0, 0);
            return true;
        }

        if (m.Groups["h24"].Success)
        {
            return TryMakeTime(int.Parse(m.Groups["h24"].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups["m24"].Value, CultureInfo.InvariantCulture), out time);
        }

        var hour = int.Parse(m.Groups["h"].Value, CultureInfo.InvariantCulture);
        var minute = m.Groups["m"].Success ? int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
        if (hour is < 1 or > 12) return false;
        var pm = m.Groups["ampm"].Value == "pm";
        hour = hour % 12 + (pm ? 12 : 0);
        return TryMakeTime(hour, minute, out time);
    }

    private static bool TryMakeTime(int hour, int minute, out TimeOnly time)
    {
        time = default;
        if (hour is < 0 or > 23 || minute is < 0 or > 59) return false;
        time = new TimeOnly(hour, minute);
        return true;
    }

    #endregion

    #region Display

    // Friendly English: "2 hours before", "day of at 12:00 PM", "2 days before at 9:00 AM"
    public string Describe()
    {
        if (AtTime is { } at)
        {
            var clock = at.ToString("h:mm tt", CultureInfo.InvariantCulture);
            return Days switch
            {
                0 => $"day of at {clock}",
                -1 => $"day before at {clock}",
                1 => $"day after at {clock}",
                < 0 => $"{-Days} days before at {clock}",
                _ => $"{Days} days after at {clock}"
            };
        }

        if (Days == 0 && Hours == 0 && Minutes == 0)
        {
            return "at start";
        }

        var parts = new List<string>();
        if (Days != 0) parts.Add(Plural(Math.Abs(Days), "day"));
        if (Hours != 0) parts.Add(Plural(Math.Abs(Hours), "hour"));
        if (Minutes != 0) parts.Add(Plural(Math.Abs(Minutes), "min", "min"));
        var direction = Days < 0 || Hours < 0 || Minutes < 0 ? "before" : "after";
        return $"{string.Join(" ", parts)} {direction}";
    }

    private static string Plural(int n, string unit, string? plural = null) =>
        n == 1 ? $"1 {unit}" : $"{n} {plural ?? unit + "s"}";

    public override string ToString() => Describe();

    #endregion

    #region Equality

    public bool Equals(DueOffset? other) =>
        other is not null && Days == other.Days && Hours == other.Hours && Minutes == other.Minutes && AtTime == other.AtTime;

    public override bool Equals(object? obj) => Equals(obj as DueOffset);

    public override int GetHashCode() => HashCode.Combine(Days, Hours, Minutes, AtTime);

    #endregion
}
