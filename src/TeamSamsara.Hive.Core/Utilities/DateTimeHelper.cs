namespace TeamSamsara.Hive.Core.Utilities;

// DateTimeHelper: Common date/time operations
// Static helper methods for working with dates and times
// Used throughout the application for consistency
public static class DateTimeHelper
{
    // Get current UTC time
    // Use this instead of DateTime.UtcNow for consistency
    public static DateTime GetUtcNow()
    {
        return DateTime.UtcNow;
    }
    
    // Get the start of a day (midnight)
    // Example: 2026-01-30 16:37:00 -> 2026-01-30 00:00:00
    public static DateTime StartOfDay(DateTime date)
    {
        return date.Date;
    }
    
    // Get the end of a day (23:59:59.999...)
    // Example: 2026-01-30 16:37:00 -> 2026-01-30 23:59:59.999...
    public static DateTime EndOfDay(DateTime date)
    {
        return date.Date.AddDays(1).AddTicks(-1);
    }
    
    // Get the start of a month (first day at midnight)
    // Example: 2026-01-30 16:37:00 -> 2026-01-01 00:00:00
    public static DateTime StartOfMonth(DateTime date)
    {
        return new DateTime(date.Year, date.Month, 1);
    }
    
    // Get the end of a month (last day at 23:59:59.999...)
    // Example: 2026-01-30 16:37:00 -> 2026-01-31 23:59:59.999...
    public static DateTime EndOfMonth(DateTime date)
    {
        return StartOfMonth(date).AddMonths(1).AddTicks(-1);
    }
    
    // Check if a date is today (UTC)
    public static bool IsToday(DateTime date)
    {
        var today = DateTime.UtcNow.Date;
        return date.Date == today;
    }
    
    // Get number of days between two dates
    public static int DaysBetween(DateTime start, DateTime end)
    {
        return (end.Date - start.Date).Days;
    }
    
    // Format timestamp for logging (consistent format across application)
    // Example: "2026-01-30 16:37:45 UTC"
    public static string FormatForLogging(DateTime dateTime)
    {
        return dateTime.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
    }
}