namespace SalsCaterSuite.Infrastructure.BackgroundJobs;

/// <summary>
/// BG-04 poll settings. The recommended cadence is one minute: event start and pack-out are
/// minute-resolution, and a one-minute poll keeps the transition inside that resolution
/// without a scheduler package.
/// </summary>
public sealed class BookingLifecycleOptions
{
    public const string SectionName = "BookingLifecycle";

    public const int RecommendedPollIntervalMinutes = 1;

    public const int DefaultBatchSize = 100;

    /// <summary>When false the hosted service does not poll. The transition service remains callable.</summary>
    public bool Enabled { get; set; } = false;

    public int PollIntervalMinutes { get; set; } = RecommendedPollIntervalMinutes;

    public int BatchSize { get; set; } = DefaultBatchSize;

    public TimeSpan PollInterval => TimeSpan.FromMinutes(Math.Max(1, PollIntervalMinutes));
}
