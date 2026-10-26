using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Logging;

namespace TeamSamsara.Hive.Bot.Database.Firestore;

// Creates the single FirestoreDb client the whole bot shares (it is thread-safe).
//
// Credentials are looked up in this order:
//   1. Database:Firestore:CredentialsJson - the key file's contents
//      (handy for hosts that only offer environment variables)
//   2. Database:Firestore:CredentialsPath - path to the key file
//   3. GOOGLE_APPLICATION_CREDENTIALS     - Google's standard env var holding a path
//
// The key file is a full-access password to the database: keep it out of
// the repository and out of chat.
public static class FirestoreFactory
{
    public static FirestoreDb Create(DatabaseSettings settings, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(settings.FirestoreProjectId))
        {
            throw new InvalidOperationException(
                "Database:Firestore:ProjectId is not configured.");
        }

        var builder = new FirestoreDbBuilder
        {
            ProjectId = settings.FirestoreProjectId
        };

        string source;
        if (settings.FirestoreCredentialsJson != null)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(settings.FirestoreCredentialsJson));
            builder.GoogleCredential = LoadServiceAccount(stream);
            source = "Database:Firestore:CredentialsJson";
        }
        else if (settings.FirestoreCredentialsPath != null)
        {
            if (!File.Exists(settings.FirestoreCredentialsPath))
            {
                throw new FileNotFoundException(
                    "Firestore key file not found (Database:Firestore:CredentialsPath).",
                    settings.FirestoreCredentialsPath);
            }

            using var stream = File.OpenRead(settings.FirestoreCredentialsPath);
            builder.GoogleCredential = LoadServiceAccount(stream);
            source = "Database:Firestore:CredentialsPath";
        }
        else if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")))
        {
            // The client library reads this variable itself
            source = "GOOGLE_APPLICATION_CREDENTIALS";
        }
        else
        {
            throw new InvalidOperationException(
                "No Firestore credentials configured. Set Database:Firestore:CredentialsPath, " +
                "Database:Firestore:CredentialsJson, or the GOOGLE_APPLICATION_CREDENTIALS environment variable.");
        }

        var db = builder.Build();
        logger.LogInformation("[Firestore] Client created for project {ProjectId} (credentials from {Source})",
            settings.FirestoreProjectId, source);
        return db;
    }

    // Only accepts a service account key (the file Firebase generates), which
    // is why Google recommends this over the generic, deprecated loaders.
    private static GoogleCredential LoadServiceAccount(Stream keyFile) =>
        GoogleCredential.FromServiceAccountCredential(ServiceAccountCredential.FromServiceAccountData(keyFile));

    // Makes one tiny read so a wrong key, a wrong project ID or a missing
    // database fails loudly at startup instead of on the first command.
    public static async Task VerifyConnectionAsync(FirestoreDb db, CancellationToken cancellationToken = default)
    {
        await db.Collection(FirestoreCollections.Configuration).Limit(1).GetSnapshotAsync(cancellationToken);
    }
}
