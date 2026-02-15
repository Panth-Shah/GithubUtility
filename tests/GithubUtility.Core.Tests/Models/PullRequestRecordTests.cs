using FluentAssertions;
using GithubUtility.Core.Models;
using Xunit;

namespace GithubUtility.Core.Tests.Models;

public class PullRequestRecordTests
{
    [Fact]
    public void PullRequestRecord_WithValidData_CreatesSuccessfully()
    {
        // Arrange & Act
        var record = new PullRequestRecord(
            "repo1",
            123,
            "Test PR",
            "author1",
            PullRequestState.Open,
            DateTimeOffset.UtcNow.AddDays(-5),
            DateTimeOffset.UtcNow,
            null,
            Array.Empty<PullRequestReviewRecord>(),
            Array.Empty<PullRequestEventRecord>());

        // Assert
        record.Should().NotBeNull();
        record.Repository.Should().Be("repo1");
        record.Number.Should().Be(123);
        record.Title.Should().Be("Test PR");
        record.Author.Should().Be("author1");
        record.State.Should().Be(PullRequestState.Open);
        record.MergedAt.Should().BeNull();
    }

    [Fact]
    public void PullRequestRecord_WithMergedState_HasMergedAt()
    {
        // Arrange
        var mergedAt = DateTimeOffset.UtcNow;

        // Act
        var record = new PullRequestRecord(
            "repo1",
            123,
            "Test PR",
            "author1",
            PullRequestState.Merged,
            DateTimeOffset.UtcNow.AddDays(-5),
            DateTimeOffset.UtcNow,
            mergedAt,
            Array.Empty<PullRequestReviewRecord>(),
            Array.Empty<PullRequestEventRecord>());

        // Assert
        record.State.Should().Be(PullRequestState.Merged);
        record.MergedAt.Should().Be(mergedAt);
    }

    [Fact]
    public void PullRequestRecord_WithReviews_ContainsReviews()
    {
        // Arrange
        var reviews = new List<PullRequestReviewRecord>
        {
            new("reviewer1", "APPROVED", DateTimeOffset.UtcNow),
            new("reviewer2", "CHANGES_REQUESTED", DateTimeOffset.UtcNow)
        };

        // Act
        var record = new PullRequestRecord(
            "repo1",
            123,
            "Test PR",
            "author1",
            PullRequestState.Open,
            DateTimeOffset.UtcNow.AddDays(-5),
            DateTimeOffset.UtcNow,
            null,
            reviews,
            Array.Empty<PullRequestEventRecord>());

        // Assert
        record.Reviews.Should().HaveCount(2);
        record.Reviews[0].Reviewer.Should().Be("reviewer1");
        record.Reviews[0].State.Should().Be("APPROVED");
    }
}
