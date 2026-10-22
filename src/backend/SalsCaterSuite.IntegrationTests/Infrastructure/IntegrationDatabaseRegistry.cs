namespace SalsCaterSuite.IntegrationTests.Infrastructure;

/// <summary>
/// Records the disposable integration databases created and dropped by a test run so the evidence
/// can be reported without scraping the LocalDB instance afterwards. Entries are written to
/// <c>integration-databases.txt</c> in the test output directory and echoed to the console.
/// </summary>
internal static class IntegrationDatabaseRegistry
{
    private static readonly object Gate = new();
    private static readonly string LogPath =
        Path.Combine(AppContext.BaseDirectory, "integration-databases.txt");

    public static void Record(string action, string databaseName)
    {
        var line = $"{DateTime.UtcNow:O}\t{action}\t{databaseName}";

        lock (Gate)
        {
            try
            {
                File.AppendAllLines(LogPath, new[] { line });
            }
            catch (IOException)
            {
                // Evidence logging must never fail a test run.
            }
        }

        Console.WriteLine($"[integration-database] {line}");
    }
}
