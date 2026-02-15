using FluentAssertions;
using GithubUtility.Core.Abstractions;
using GithubUtility.Core.Models;
using GithubUtility.Core.Services;
using Moq;
using Xunit;

namespace GithubUtility.Core.Tests.Services;

public class PrAuditOrchestratorTests
{
    private readonly Mock<IGitHubDataSource> _mockDataSource;
    private readonly Mock<IAuditRepository> _mockRepository;
    private readonly PrAuditOrchestrator _orchestrator;

    public PrAuditOrchestratorTests()
    {
        _mockDataSource = new Mock<IGitHubDataSource>();
        _mockRepository = new Mock<IAuditRepository>();
        _orchestrator = new PrAuditOrchestrator(_mockDataSource.Object, _mockRepository.Object);
    }

    [Fact]
    public async Task RunIngestionAsync_WithNoRepositories_ReturnsEmptyResult()
    {
        // Arrange
        _mockDataSource.Setup(x => x.ListRepositoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());

        // Act
        var result = await _orchestrator.RunIngestionAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RepositoryCount.Should().Be(0);
        result.PullRequestCount.Should().Be(0);
        result.ErrorCount.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task RunIngestionAsync_WithRepositories_ProcessesAllRepositories()
    {
        // Arrange
        var repositories = new[] { "repo1", "repo2" };
        var prs = new List<PullRequestRecord>
        {
            new("repo1", 1, "PR 1", "author1", PullRequestState.Open, 
                DateTimeOffset.UtcNow.AddDays(-5), DateTimeOffset.UtcNow, null, 
                Array.Empty<PullRequestReviewRecord>(), Array.Empty<PullRequestEventRecord>())
        };

        _mockDataSource.Setup(x => x.ListRepositoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(repositories);
        _mockRepository.Setup(x => x.GetCursorAsync("repo1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepositoryCursor?)null);
        _mockRepository.Setup(x => x.GetCursorAsync("repo2", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepositoryCursor?)null);
        _mockDataSource.Setup(x => x.ListPullRequestsUpdatedSinceAsync("repo1", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prs);
        _mockDataSource.Setup(x => x.ListPullRequestsUpdatedSinceAsync("repo2", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PullRequestRecord>());

        // Act
        var result = await _orchestrator.RunIngestionAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RepositoryCount.Should().Be(2);
        result.PullRequestCount.Should().Be(1);
        result.ErrorCount.Should().Be(0);
        _mockRepository.Verify(x => x.UpsertPullRequestsAsync(It.IsAny<IReadOnlyList<PullRequestRecord>>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockRepository.Verify(x => x.SaveCursorAsync(It.IsAny<RepositoryCursor>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RunIngestionAsync_WithErrors_CollectsErrors()
    {
        // Arrange
        var repositories = new[] { "repo1", "repo2" };
        _mockDataSource.Setup(x => x.ListRepositoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(repositories);
        _mockRepository.Setup(x => x.GetCursorAsync("repo1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));
        _mockRepository.Setup(x => x.GetCursorAsync("repo2", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RepositoryCursor?)null);
        _mockDataSource.Setup(x => x.ListPullRequestsUpdatedSinceAsync("repo2", It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PullRequestRecord>());

        // Act
        var result = await _orchestrator.RunIngestionAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RepositoryCount.Should().Be(2);
        result.ErrorCount.Should().Be(1);
        result.Errors.Should().Contain(e => e.Contains("repo1") && e.Contains("Database error"));
    }

    [Fact]
    public async Task GetOpenPrReportAsync_WithNoFilter_ReturnsAllOpenPRs()
    {
        // Arrange
        var prs = new List<PullRequestRecord>
        {
            new("repo1", 1, "PR 1", "author1", PullRequestState.Open, 
                DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow, null, 
                Array.Empty<PullRequestReviewRecord>(), Array.Empty<PullRequestEventRecord>()),
            new("repo2", 2, "PR 2", "author2", PullRequestState.Open, 
                DateTimeOffset.UtcNow.AddDays(-5), DateTimeOffset.UtcNow, null, 
                Array.Empty<PullRequestReviewRecord>(), Array.Empty<PullRequestEventRecord>())
        };

        _mockRepository.Setup(x => x.ListPullRequestsByStateAsync(PullRequestState.Open, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prs);

        // Act
        var result = await _orchestrator.GetOpenPrReportAsync(null, 0, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result[0].Repository.Should().Be("repo1");
        result[0].AgeDays.Should().Be(10);
        result[1].Repository.Should().Be("repo2");
        result[1].AgeDays.Should().Be(5);
    }

    [Fact]
    public async Task GetOpenPrReportAsync_WithRepositoryFilter_FiltersByRepository()
    {
        // Arrange
        var prs = new List<PullRequestRecord>
        {
            new("repo1", 1, "PR 1", "author1", PullRequestState.Open, 
                DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow, null, 
                Array.Empty<PullRequestReviewRecord>(), Array.Empty<PullRequestEventRecord>())
        };

        _mockRepository.Setup(x => x.ListPullRequestsByStateAsync(PullRequestState.Open, "repo1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(prs);

        // Act
        var result = await _orchestrator.GetOpenPrReportAsync("repo1", 0, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].Repository.Should().Be("repo1");
    }

    [Fact]
    public async Task GetOpenPrReportAsync_WithOlderThanDaysFilter_FiltersByAge()
    {
        // Arrange
        var prs = new List<PullRequestRecord>
        {
            new("repo1", 1, "PR 1", "author1", PullRequestState.Open, 
                DateTimeOffset.UtcNow.AddDays(-10), DateTimeOffset.UtcNow, null, 
                Array.Empty<PullRequestReviewRecord>(), Array.Empty<PullRequestEventRecord>()),
            new("repo2", 2, "PR 2", "author2", PullRequestState.Open, 
                DateTimeOffset.UtcNow.AddDays(-5), DateTimeOffset.UtcNow, null, 
                Array.Empty<PullRequestReviewRecord>(), Array.Empty<PullRequestEventRecord>())
        };

        _mockRepository.Setup(x => x.ListPullRequestsByStateAsync(PullRequestState.Open, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prs);

        // Act
        var result = await _orchestrator.GetOpenPrReportAsync(null, 7, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].Repository.Should().Be("repo1");
        result[0].AgeDays.Should().Be(10);
    }

    [Fact]
    public async Task GetUserStatsAsync_CalculatesStatsCorrectly()
    {
        // Arrange
        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var to = DateTimeOffset.UtcNow;
        var prs = new List<PullRequestRecord>
        {
            new("repo1", 1, "PR 1", "author1", PullRequestState.Merged, 
                from.AddDays(1), from.AddDays(2), from.AddDays(2), 
                new List<PullRequestReviewRecord>
                {
                    new("reviewer1", "APPROVED", from.AddDays(1))
                },
                Array.Empty<PullRequestEventRecord>()),
            new("repo2", 2, "PR 2", "author1", PullRequestState.Open, 
                from.AddDays(3), from.AddDays(4), null, 
                Array.Empty<PullRequestReviewRecord>(), 
                Array.Empty<PullRequestEventRecord>())
        };

        _mockRepository.Setup(x => x.ListPullRequestsByDateRangeAsync(from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prs);

        // Act
        var result = await _orchestrator.GetUserStatsAsync(from, to, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2); // author1 and reviewer1
        var author1 = result.FirstOrDefault(r => r.User == "author1");
        author1.Should().NotBeNull();
        author1!.OpenedPrs.Should().Be(2);
        author1.MergedPrs.Should().Be(1);
        author1.ReviewsSubmitted.Should().Be(0);
        author1.ApprovalsSubmitted.Should().Be(0);

        var reviewer1 = result.FirstOrDefault(r => r.User == "reviewer1");
        reviewer1.Should().NotBeNull();
        reviewer1!.OpenedPrs.Should().Be(0);
        reviewer1.MergedPrs.Should().Be(0);
        reviewer1.ReviewsSubmitted.Should().Be(1);
        reviewer1.ApprovalsSubmitted.Should().Be(1);
    }

    [Fact]
    public async Task GetReleaseAuditSummaryAsync_CalculatesSummaryCorrectly()
    {
        // Arrange
        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var to = DateTimeOffset.UtcNow;
        var prs = new List<PullRequestRecord>
        {
            new("repo1", 1, "PR 1", "author1", PullRequestState.Open, 
                from.AddDays(1), from.AddDays(2), null, 
                Array.Empty<PullRequestReviewRecord>(), Array.Empty<PullRequestEventRecord>()),
            new("repo2", 2, "PR 2", "author2", PullRequestState.Merged, 
                from.AddDays(3), from.AddDays(4), from.AddDays(4), 
                Array.Empty<PullRequestReviewRecord>(), Array.Empty<PullRequestEventRecord>()),
            new("repo3", 3, "PR 3", "author3", PullRequestState.Closed, 
                from.AddDays(5), from.AddDays(6), null, 
                Array.Empty<PullRequestReviewRecord>(), Array.Empty<PullRequestEventRecord>())
        };

        _mockRepository.Setup(x => x.ListPullRequestsByDateRangeAsync(from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prs);

        // Act
        var result = await _orchestrator.GetReleaseAuditSummaryAsync(from, to, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.From.Should().Be(from);
        result.To.Should().Be(to);
        result.OpenPrs.Should().Be(1);
        result.ClosedPrs.Should().Be(1);
        result.MergedPrs.Should().Be(1);
        result.MergedWithoutApproval.Should().Be(1); // PR 2 has no approvals
    }

    [Fact]
    public async Task GetRepositoryReportAsync_GroupsByRepository()
    {
        // Arrange
        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var to = DateTimeOffset.UtcNow;
        var prs = new List<PullRequestRecord>
        {
            new("repo1", 1, "PR 1", "author1", PullRequestState.Open, 
                from.AddDays(1), from.AddDays(2), null, 
                Array.Empty<PullRequestReviewRecord>(), Array.Empty<PullRequestEventRecord>()),
            new("repo1", 2, "PR 2", "author2", PullRequestState.Merged, 
                from.AddDays(3), from.AddDays(4), from.AddDays(4), 
                Array.Empty<PullRequestReviewRecord>(), Array.Empty<PullRequestEventRecord>()),
            new("repo2", 3, "PR 3", "author3", PullRequestState.Closed, 
                from.AddDays(5), from.AddDays(6), null, 
                Array.Empty<PullRequestReviewRecord>(), Array.Empty<PullRequestEventRecord>())
        };

        _mockRepository.Setup(x => x.ListPullRequestsByDateRangeAsync(from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(prs);

        // Act
        var result = await _orchestrator.GetRepositoryReportAsync(from, to, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        var repo1 = result.FirstOrDefault(r => r.Repository == "repo1");
        repo1.Should().NotBeNull();
        repo1!.OpenPrs.Should().Be(1);
        repo1.MergedPrs.Should().Be(1);
        repo1.ClosedPrs.Should().Be(0);
        repo1.ActiveContributors.Should().Be(2);
    }
}
