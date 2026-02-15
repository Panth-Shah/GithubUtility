using GithubUtility.App.Options;
using GithubUtility.Core.Abstractions;
using Microsoft.Extensions.Options;

namespace GithubUtility.App.Workers;

public sealed class ScheduledIngestionWorker(
    IPrAuditOrchestrator orchestrator,
    IOptions<SchedulerOptions> options,
    ILogger<ScheduledIngestionWorker> logger) : BackgroundService
{
    private readonly SchedulerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(_options.IngestionIntervalMinutes, 1));
        using var timer = new PeriodicTimer(interval);

        logger.LogInformation("Scheduled ingestion worker started with interval {IntervalMinutes} minute(s).", interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await orchestrator.RunIngestionAsync(stoppingToken);
                logger.LogInformation(
                    "Ingestion completed. Repositories: {RepositoryCount}, PRs: {PrCount}, Errors: {ErrorCount}.",
                    result.RepositoryCount,
                    result.PullRequestCount,
                    result.ErrorCount);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled ingestion failed.");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
