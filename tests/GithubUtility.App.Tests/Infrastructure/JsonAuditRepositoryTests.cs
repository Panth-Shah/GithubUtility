using FluentAssertions;
using GithubUtility.App.Infrastructure;
using GithubUtility.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace GithubUtility.App.Tests.Infrastructure;

public class JsonAuditRepositoryTests : IDisposable
{
    private readonly string _testDataPath;
    private readonly JsonAuditRepository _repository;

    public JsonAuditRepositoryTests()
    {
        _testDataPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDataPath);

        var mockHostEnvironment = new Mock<IHostEnvironment>();
        mockHostEnvironment.Setup(x => x.ContentRootPath).Returns(_testDataPath);

        _repository = new JsonAuditRepository(mockHostEnvironment.Object);
    }

    [Fact]
    public async Task GetCursorAsync_WithNoExistingCursor_ReturnsNull()
    {
        // Act
        var result = await _repository.GetCursorAsync("repo1", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveCursorAsync_AndGetCursorAsync_ReturnsSavedCursor()
    {
        // Arrange
        var cursor = new RepositoryCursor("repo1", DateTimeOffset.UtcNow);

        // Act
        await _repository.SaveCursorAsync(cursor, CancellationToken.None);
        var result = await _repository.GetCursorAsync("repo1", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Repository.Should().Be("repo1");
        result.LastSuccessfulSyncUtc.Should().BeCloseTo(cursor.LastSuccessfulSyncUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task UpsertPullRequestsAsync_AndListPullRequestsAsync_ReturnsSavedPRs()
    {
        // Arrange
        var prs = new List<PullRequestRecord>
        {
            new("repo1", 1, "PR 1", "author1", PullRequestState.Open,
                DateTimeOffset.UtcNow.AddDays(-5), DateTimeOffset.UtcNow, null,
                Array.Empty<PullRequestReviewRecord>(), Array.Empty<PullRequestEventRecord>()),
            new("repo1", 2, "PR 2", "author2", PullRequestState.Merged,
                DateTimeOffset.UtcNow.AddDays(-3), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                Array.Empty<PullRequestReviewRecord>(), Array.Empty<PullRequestEventRecord>())
        };

        // Act
        await _repository.UpsertPullRequestsAsync(prs, CancellationToken.None);
        var result = await _repository.ListPullRequestsAsync(CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].Number.Should().Be(1);
        result[1].Number.Should().Be(2);
    }

    [Fact]
    public async Task UpsertPullRequestsAsync_WithDuplicatePR_UpdatesExisting()
    {
        // Arrange
        var pr1 = new PullRequestRecord("repo1", 1, "PR 1", "author1", PullRequestState.Open,
            DateTimeOffset.UtcNow.AddDays(-5), DateTimeOffset.UtcNow, null,
            Array.Empty<PullRequestReviewRecord>(), Array.Empty<PullRequestEventRecord>());
        var pr1Updated = new PullRequestRecord("repo1", 1, "PR 1 Updated", "author1", PullRequestState.Merged,
            DateTimeOffset.UtcNow.AddDays(-5), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            Array.Empty<PullRequestReviewRecord>(), Array.Empty<PullRequestEventRecord>());

        // Act
        await _repository.UpsertPullRequestsAsync(new[] { pr1 }, CancellationToken.None);
        await _repository.UpsertPullRequestsAsync(new[] { pr1Updated }, CancellationToken.None);
        var result = await _repository.ListPullRequestsAsync(CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Title.Should().Be("PR 1 Updated");
        result[0].State.Should().Be(PullRequestState.Merged);
    }

    [Fact]
    public async Task ListPullRequestsByStateAsync_FiltersByState()
    {
        // Arrange
        var prs = new List<PullRequestRecord>
        {
            new("repo1", 1, "PR 1", "author1", PullRequestState.Open,
                DateTimeOffset.UtcNow.AddDays(-5), DateTimeOffset.UtcNow, null,
                Array.Empty<PullRequestReviewRecord>(), Array.Empty<PullRequestEventRecord>()),
            new("repo1", 2, "PR 2", "author2", PullRequestState.Merged,
                DateTimeOffset.UtcNow.AddDays(-3), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                Array.Empty<PullRequestReviewRecord>(), Array.Empty<PullRequestEventRecord>())
        };
        await _repository.UpsertPullRequestsAsync(prs, CancellationToken.None);

        // Act
        var openPrs = await _repository.ListPullRequestsByStateAsync(PullRequestState.Open, null, CancellationToken.None);
        var mergedPrs = await _repository.ListPullRequestsByStateAsync(PullRequestState.Merged, null, CancellationToken.None);

        // Assert
        openPrs.Should().HaveCount(1);
        openPrs[0].Number.Should().Be(1);
        mergedPrs.Should().HaveCount(1);
        mergedPrs[0].Number.Should().Be(2);
    }

    [Fact]
    public async Task ListPullRequestsByDateRangeAsync_FiltersByDateRange()
    {
        // Arrange
        var from = DateTimeOffset.UtcNow.AddDays(-10);
        var to = DateTimeOffset.UtcNow;
        var prs = new List<PullRequestRecord>
        {
            new("repo1", 1, "PR 1", "author1", PullRequestState.Open,
                from.AddDays(-5), from.AddDays(-5), null,
                Array.Empty<PullRequestReviewRecord>(), Array.Empty<PullRequestEventRecord>()),
            new("repo1", 2, "PR 2", "author2", PullRequestState.Merged,
                from.AddDays(1), from.AddDays(1), from.AddDays(1),
                Array.Empty<PullRequestReviewRecord>(), Array.Empty<PullRequestEventRecord>())
        };
        await _repository.UpsertPullRequestsAsync(prs, CancellationToken.None);

        // Act
        var result = await _repository.ListPullRequestsByDateRangeAsync(from, to, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result[0].Number.Should().Be(2);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataPath))
        {
            Directory.Delete(_testDataPath, true);
        }
    }
}
