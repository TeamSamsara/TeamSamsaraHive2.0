using System.Globalization;
using Google.Cloud.Firestore;

namespace TeamSamsara.Hive.Bot.Database.Firestore;

// Collection names, in one place
public static class FirestoreCollections
{
    public const string TrackedStreams = "trackedStreams";
    public const string Configuration = "configuration";
    public const string Tickets = "tickets";
    public const string StatusTransitions = "statusTransitions";
    public const string Counters = "counters";
}

// Conversions between entity property types and Firestore field values.
//
// Repositories map entities to plain dictionaries by hand (instead of putting
// Firestore attributes on the entities) so the same entity classes keep
// working unchanged with SQLite / Entity Framework.
//
//   ulong (Discord IDs) -> string   (Discord IDs are huge; text avoids any number-size issues)
//   Guid                -> string
//   enum                -> long     (numeric, so "priority >= High" and sorting still work)
//   DateTime            -> Timestamp (always stored as UTC)
public static class FirestoreValues
{
    #region Writing

    public static string FromULong(ulong value) => value.ToString(CultureInfo.InvariantCulture);

    public static string? FromULong(ulong? value) => value?.ToString(CultureInfo.InvariantCulture);

    public static string FromGuid(Guid value) => value.ToString("D");

    public static long FromEnum<TEnum>(TEnum value) where TEnum : struct, Enum => Convert.ToInt64(value);

    public static DateTime FromDateTime(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        // Unspecified: every timestamp in this bot is created as UTC
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    public static DateTime? FromDateTime(DateTime? value) => value.HasValue ? FromDateTime(value.Value) : null;

    #endregion

    #region Reading

    public static string GetString(DocumentSnapshot doc, string field, string fallback = "") =>
        doc.TryGetValue<string?>(field, out var value) && value != null ? value : fallback;

    public static string? GetNullableString(DocumentSnapshot doc, string field) =>
        doc.TryGetValue<string?>(field, out var value) ? value : null;

    public static bool GetBool(DocumentSnapshot doc, string field) =>
        doc.TryGetValue<bool?>(field, out var value) && value == true;

    public static ulong GetULong(DocumentSnapshot doc, string field) =>
        GetNullableULong(doc, field) ?? 0;

    public static ulong? GetNullableULong(DocumentSnapshot doc, string field)
    {
        var text = GetNullableString(doc, field);
        return ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    public static Guid GetGuid(DocumentSnapshot doc, string field, Guid fallback)
    {
        var text = GetNullableString(doc, field);
        return Guid.TryParse(text, out var value) ? value : fallback;
    }

    public static TEnum GetEnum<TEnum>(DocumentSnapshot doc, string field, TEnum fallback) where TEnum : struct, Enum
    {
        if (!doc.TryGetValue<long?>(field, out var number) || number == null)
        {
            return fallback;
        }

        var value = (TEnum)Enum.ToObject(typeof(TEnum), number.Value);
        return Enum.IsDefined(value) ? value : fallback;
    }

    public static DateTime GetDateTime(DocumentSnapshot doc, string field) =>
        GetNullableDateTime(doc, field) ?? DateTime.MinValue;

    public static DateTime? GetNullableDateTime(DocumentSnapshot doc, string field) =>
        doc.TryGetValue<DateTime?>(field, out var value) && value.HasValue
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : null;

    #endregion
}
