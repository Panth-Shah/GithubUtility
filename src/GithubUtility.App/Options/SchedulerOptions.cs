using System.ComponentModel.DataAnnotations;

namespace GithubUtility.App.Options;

public sealed class SchedulerOptions
{
    public const string SectionName = "Scheduler";

    [Range(1, 1440, ErrorMessage = "IngestionIntervalMinutes must be between 1 and 1440 (24 hours)")]
    public int IngestionIntervalMinutes { get; init; } = 60;
}
