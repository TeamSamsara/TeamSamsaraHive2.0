namespace TeamSamsara.Hive.Core.Utilities;

// StringHelper: Common string operations
// Static helper methods for working with strings
// Used throughout the application for validation and manipulation
public static class StringHelper
{
    // Check if string is null or empty
    public static bool IsNullOrEmpty(string? value)
    {
        return string.IsNullOrEmpty(value);
    }
    
    // Check if string is null, empty, or only whitespace
    public static bool IsNullOrWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value);
    }
    
    // Convert string to lowercase (culture-invariant)
    // Returns empty string if input is null
    public static string ToLowerInvariant(string? value)
    {
        return value?.ToLowerInvariant() ?? string.Empty;
    }
    
    // Convert string to uppercase (culture-invariant)
    // Returns empty string if input is null
    public static string ToUpperInvariant(string? value)
    {
        return value?.ToUpperInvariant() ?? string.Empty;
    }
    
    // Truncate string to maximum length
    // Returns empty string if input is null
    // Example: Truncate("Hello World", 5) -> "Hello"
    public static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }
    
    // Truncate with ellipsis (...)
    // Example: TruncateWithEllipsis("Hello World", 8) -> "Hello..."
    public static string TruncateWithEllipsis(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= maxLength) return value;
        
        // Reserve 3 characters for "..."
        int cutLength = Math.Max(0, maxLength - 3);
        return value.Substring(0, cutLength) + "...";
    }
    
    // Remove extra whitespace (trim and reduce multiple spaces to single space)
    // Example: "  Hello    World  " -> "Hello World"
    public static string NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        
        // Split by whitespace and rejoin with single spaces
        var words = value.Split(new[] { ' ', '\t', '\n', '\r' }, 
            StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words);
    }
    
    // Safe substring (won't throw if indices are out of range)
    public static string SafeSubstring(string? value, int startIndex, int length)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (startIndex >= value.Length) return string.Empty;
        
        int actualLength = Math.Min(length, value.Length - startIndex);
        return value.Substring(startIndex, actualLength);
    }
    
    // Check if string contains another string (case-insensitive)
    public static bool ContainsIgnoreCase(string? source, string? value)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
            return false;
        
        return source.Contains(value, StringComparison.OrdinalIgnoreCase);
    }
    
    // Sanitize for Discord (remove @everyone, @here mentions to prevent spam)
    // Useful for posting user input to Discord
    public static string SanitizeForDiscord(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        
        return value
            .Replace("@everyone", "@\u200Beveryone")  // Zero-width space
            .Replace("@here", "@\u200Bhere");
    }
}