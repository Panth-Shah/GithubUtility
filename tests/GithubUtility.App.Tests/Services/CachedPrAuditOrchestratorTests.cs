using FluentAssertions;
using GithubUtility.Core.Abstractions;
using GithubUtility.Core.Models;
using GithubUtility.App.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GithubUtility.App.Tests.Services;

public class CachedPrAuditOrchestratorTests
{
    private readonly Mock<IPrAuditOrchestrator> _mockInner;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<CachedPrAuditOrchestrator>> _mockLogger;
    private readonly CachedPrAuditOrchestrator _cachedOrchestrator;

    public CachedPrAuditOrchestratorTests()
    {
        _mockInner = new Mock<IPrAuditOrchestrator>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<CachedPrAuditOrchestrator>>();
        _cachedOrchestrator = new CachedPrAuditOrchestrator(_mockInner.Object, _cache, _mockLogger.Object);
    }

    [Fact]
    public async Task GetOpenPrReportAsync_FirstCall_CallsInnerAndCaches()
    {
        // Arrange
        var expected = new List<OpenPrSummary>
        {
            new("repo1", 1, "PR 1", "author1", 5, DateTimeOffset.UtcNow)
        };
        _mockInner.Setup(x => x.GetOpenPrReportAsync(null, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result1 = await _cachedOrchestrator.GetOpenPrReportAsync(null, 0, CancellationToken.None);
        var result2 = await _cachedOrchestrator.GetOpenPrReportAsync(null, 0, CancellationToken.None);

        // Assert
        result1.Should().BeEquivalentTo(expected);
        result2.Should().BeEquivalentTo(expected);
        _mockInner.Verify(x => x.GetOpenPrReportAsync(null, 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUserStatsAsync_FirstCall_CallsInnerAndCaches()
    {
        // Arrange
        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var to = DateTimeOffset.UtcNow;
        var expected = new List<UserStatSummary>
        {
            new("user1", 5, 3, 10, 8) // OpenedPrs, MergedPrs, ReviewsSubmitted, ApprovalsSubmitted
        };
        _mockInner.Setup(x => x.GetUserStatsAsync(from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result1 = await _cachedOrchestrator.GetUserStatsAsync(from, to, CancellationToken.None);
        var result2 = await _cachedOrchestrator.GetUserStatsAsync(from, to, CancellationToken.None);

        // Assert
        result1.Should().BeEquivalentTo(expected);
        result2.Should().BeEquivalentTo(expected);
        _mockInner.Verify(x => x.GetUserStatsAsync(from, to, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetReleaseAuditSummaryAsync_FirstCall_CallsInnerAndCaches()
    {
        // Arrange
        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var to = DateTimeOffset.UtcNow;
        var expected = new ReleaseAuditSummary(from, to, 5, 3, 10, 2);
        _mockInner.Setup(x => x.GetReleaseAuditSummaryAsync(from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result1 = await _cachedOrchestrator.GetReleaseAuditSummaryAsync(from, to, CancellationToken.None);
        var result2 = await _cachedOrchestrator.GetReleaseAuditSummaryAsync(from, to, CancellationToken.None);

        // Assert
        result1.Should().BeEquivalentTo(expected);
        result2.Should().BeEquivalentTo(expected);
        _mockInner.Verify(x => x.GetReleaseAuditSummaryAsync(from, to, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRepositoryReportAsync_FirstCall_CallsInnerAndCaches()
    {
        // Arrange
        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var to = DateTimeOffset.UtcNow;
        var expected = new List<RepositoryReport>
        {
            new("repo1", 2, 5, 1, 3) // OpenPrs, MergedPrs, ClosedPrs, ActiveContributors
        };
        _mockInner.Setup(x => x.GetRepositoryReportAsync(from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result1 = await _cachedOrchestrator.GetRepositoryReportAsync(from, to, CancellationToken.None);
        var result2 = await _cachedOrchestrator.GetRepositoryReportAsync(from, to, CancellationToken.None);

        // Assert
        result1.Should().BeEquivalentTo(expected);
        result2.Should().BeEquivalentTo(expected);
        _mockInner.Verify(x => x.GetRepositoryReportAsync(from, to, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunIngestionAsync_AlwaysCallsInner_DoesNotCache()
    {
        // Arrange
        var expected = new IngestionRunResult(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            2,
            5,
            0,
            Array.Empty<string>());
        _mockInner.Setup(x => x.RunIngestionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result1 = await _cachedOrchestrator.RunIngestionAsync(CancellationToken.None);
        var result2 = await _cachedOrchestrator.RunIngestionAsync(CancellationToken.None);

        // Assert
        result1.Should().BeEquivalentTo(expected);
        result2.Should().BeEquivalentTo(expected);
        _mockInner.Verify(x => x.RunIngestionAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetOpenPrReportAsync_WithDifferentParameters_UsesDifferentCacheKeys()
    {
        // Arrange
        var expected1 = new List<OpenPrSummary>
        {
            new("repo1", 1, "PR 1", "author1", 5, DateTimeOffset.UtcNow)
        };
        var expected2 = new List<OpenPrSummary>
        {
            new("repo2", 2, "PR 2", "author2", 10, DateTimeOffset.UtcNow)
        };
        _mockInner.Setup(x => x.GetOpenPrReportAsync(null, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected1);
        _mockInner.Setup(x => x.GetOpenPrReportAsync("repo1", 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected2);

        // Act
        var result1 = await _cachedOrchestrator.GetOpenPrReportAsync(null, 0, CancellationToken.None);
        var result2 = await _cachedOrchestrator.GetOpenPrReportAsync("repo1", 0, CancellationToken.None);

        // Assert
        result1.Should().BeEquivalentTo(expected1);
        result2.Should().BeEquivalentTo(expected2);
        _mockInner.Verify(x => x.GetOpenPrReportAsync(null, 0, It.IsAny<CancellationToken>()), Times.Once);
        _mockInner.Verify(x => x.GetOpenPrReportAsync("repo1", 0, It.IsAny<CancellationToken>()), Times.Once);
    }
}
