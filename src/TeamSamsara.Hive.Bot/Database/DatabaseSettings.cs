using Microsoft.Extensions.Configuration;

namespace TeamSamsara.Hive.Bot.Database;

// Which storage backend the bot uses.
//
// Every system (streams, configuration, tickets) talks to its data through a
// repository interface, so switching backend is only a matter of which
// implementation gets registered in Program.cs. Adding another backend later
// (e.g. a hosted SQL database) means adding one more set of repositories.
public enum DatabaseProvider
{
    // Local SQLite files under /data. Wiped on every restart on Render's free plan.
    Sqlite,

    // Google Cloud Firestore (Firebase). Data survives restarts and redeploys.
    Firestore
}

// Reads the "Database" section of configuration.
//
// appsettings.json:
//   "Database": {
//     "Provider": "Sqlite" | "Firestore",
//     "Firestore": {
//       "ProjectId": "tsr-hive-db",
//       "CredentialsPath": "",      // path to the service account key file
//       "CredentialsJson": "",      // OR the key file's contents (set via env var, never commit)
//       "CacheRefreshHours": 3
//     }
//   }
//
// Any value can be overridden with an environment variable using double
// underscores, e.g. Database__Provider=Firestore.
public sealed class DatabaseSettings
{
    public const string SectionName = "Database";

    public DatabaseProvider Provider { get; init; } = DatabaseProvider.Sqlite;
    public string FirestoreProjectId { get; init; } = string.Empty;
    public string? FirestoreCredentialsPath { get; init; }
    public string? FirestoreCredentialsJson { get; init; }

    // How often cached collections (tracked streams, configuration) are
    // re-read from Firestore, to pick up edits made directly in the console.
    // Changes made through the bot itself update the cache immediately.
    public TimeSpan CacheRefreshInterval { get; init; } = TimeSpan.FromHours(3);

    public static DatabaseSettings FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);

        var providerText = section["Provider"];
        var provider = DatabaseProvider.Sqlite;
        if (!string.IsNullOrWhiteSpace(providerText)
            && !Enum.TryParse(providerText, ignoreCase: true, out provider))
        {
            throw new InvalidOperationException(
                $"Unknown Database:Provider '{providerText}'. Use 'Sqlite' or 'Firestore'.");
        }

        var refreshHours = 3.0;
        var refreshText = section["Firestore:CacheRefreshHours"];
        if (!string.IsNullOrWhiteSpace(refreshText)
            && double.TryParse(refreshText, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0)
        {
            refreshHours = parsed;
        }

        return new DatabaseSettings
        {
            Provider = provider,
            FirestoreProjectId = section["Firestore:ProjectId"] ?? string.Empty,
            FirestoreCredentialsPath = NullIfBlank(section["Firestore:CredentialsPath"]),
            FirestoreCredentialsJson = NullIfBlank(section["Firestore:CredentialsJson"]),
            CacheRefreshInterval = TimeSpan.FromHours(refreshHours)
        };
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
