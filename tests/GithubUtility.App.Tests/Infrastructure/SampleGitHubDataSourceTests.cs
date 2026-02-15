using FluentAssertions;
using GithubUtility.App.Infrastructure;
using GithubUtility.App.Options;
using Microsoft.Extensions.Options;
using Xunit;

namespace GithubUtility.App.Tests.Infrastructure;

public class SampleGitHubDataSourceTests
{
    [Fact]
    public async Task ListRepositoriesAsync_WithNoConfiguredRepos_ReturnsDefaultRepos()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new GitHubConnectorOptions { Repositories = new List<string>() });
        var dataSource = new SampleGitHubDataSource(options);

        // Act
        var result = await dataSource.ListRepositoriesAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain("org/platform-service");
        result.Should().Contain("org/payments-api");
    }

    [Fact]
    public async Task ListRepositoriesAsync_WithConfiguredRepos_ReturnsConfiguredRepos()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new GitHubConnectorOptions 
        { 
            Repositories = new List<string> { "custom/repo1", "custom/repo2" } 
        });
        var dataSource = new SampleGitHubDataSource(options);

        // Act
        var result = await dataSource.ListRepositoriesAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain("custom/repo1");
        result.Should().Contain("custom/repo2");
    }

    [Fact]
    public async Task ListPullRequestsUpdatedSinceAsync_WithRecentSince_ReturnsAllPRs()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new GitHubConnectorOptions());
        var dataSource = new SampleGitHubDataSource(options);
        var since = DateTimeOffset.UtcNow.AddDays(-30);

        // Act
        var result = await dataSource.ListPullRequestsUpdatedSinceAsync("test/repo", since, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.All(pr => pr.Repository == "test/repo").Should().BeTrue();
    }

    [Fact]
    public async Task ListPullRequestsUpdatedSinceAsync_WithOldSince_ReturnsFilteredPRs()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new GitHubConnectorOptions());
        var dataSource = new SampleGitHubDataSource(options);
        var since = DateTimeOffset.UtcNow.AddDays(-1);

        // Act
        var result = await dataSource.ListPullRequestsUpdatedSinceAsync("test/repo", since, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1); // Only PR 101 was updated recently (5 hours ago)
        result[0].Number.Should().Be(101);
    }

    [Fact]
    public async Task ListPullRequestsUpdatedSinceAsync_WithFutureSince_ReturnsEmpty()
    {
        // Arrange
        var options = Microsoft.Extensions.Options.Options.Create(new GitHubConnectorOptions());
        var dataSource = new SampleGitHubDataSource(options);
        var since = DateTimeOffset.UtcNow.AddDays(1);

        // Act
        var result = await dataSource.ListPullRequestsUpdatedSinceAsync("test/repo", since, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }
}
