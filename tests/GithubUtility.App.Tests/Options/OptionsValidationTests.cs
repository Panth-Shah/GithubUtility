using FluentAssertions;
using GithubUtility.App.Options;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace GithubUtility.App.Tests.Options;

public class OptionsValidationTests
{
    [Fact]
    public void SchedulerOptions_WithValidInterval_IsValid()
    {
        // Arrange
        var options = new SchedulerOptions { IngestionIntervalMinutes = 60 };

        // Act
        var results = ValidateModel(options);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void SchedulerOptions_WithInvalidInterval_IsInvalid()
    {
        // Arrange
        var options = new SchedulerOptions { IngestionIntervalMinutes = 0 };

        // Act
        var results = ValidateModel(options);

        // Assert
        results.Should().Contain(r => r.ErrorMessage!.Contains("between 1 and 1440"));
    }

    [Fact]
    public void AuditStoreOptions_WithValidProvider_IsValid()
    {
        // Arrange
        var options = new AuditStoreOptions 
        { 
            Provider = "Sqlite",
            ConnectionString = "Data Source=test.db"
        };

        // Act
        var results = ValidateModel(options);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void AuditStoreOptions_WithInvalidProvider_IsInvalid()
    {
        // Arrange
        var options = new AuditStoreOptions 
        { 
            Provider = "Invalid",
            ConnectionString = "Data Source=test.db"
        };

        // Act
        var results = ValidateModel(options);

        // Assert
        results.Should().Contain(r => r.ErrorMessage!.Contains("Sqlite, SqlServer, or Postgres"));
    }

    [Fact]
    public void AuditStoreOptions_WithShortConnectionString_IsInvalid()
    {
        // Arrange
        var options = new AuditStoreOptions 
        { 
            Provider = "Sqlite",
            ConnectionString = "short"
        };

        // Act
        var results = ValidateModel(options);

        // Assert
        results.Should().Contain(r => r.ErrorMessage!.Contains("at least 10 characters"));
    }

    [Fact]
    public void GitHubConnectorOptions_WithValidMode_IsValid()
    {
        // Arrange
        var options = new GitHubConnectorOptions 
        { 
            Mode = "Mcp",
            Mcp = new McpConnectorOptions()
        };

        // Act
        var results = ValidateModel(options);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void GitHubConnectorOptions_WithInvalidMode_IsInvalid()
    {
        // Arrange
        var options = new GitHubConnectorOptions 
        { 
            Mode = "Invalid",
            Mcp = new McpConnectorOptions()
        };

        // Act
        var results = ValidateModel(options);

        // Assert
        results.Should().Contain(r => r.ErrorMessage!.Contains("Mcp or Sample"));
    }

    [Fact]
    public void McpConnectorOptions_WithValidEndpoint_IsValid()
    {
        // Arrange
        var options = new McpConnectorOptions 
        { 
            Endpoint = "http://localhost:8080/tools/invoke"
        };

        // Act
        var results = ValidateModel(options);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void McpConnectorOptions_WithInvalidEndpoint_IsInvalid()
    {
        // Arrange
        var options = new McpConnectorOptions 
        { 
            Endpoint = "not-a-url"
        };

        // Act
        var results = ValidateModel(options);

        // Assert
        results.Should().Contain(r => r.ErrorMessage!.Contains("valid URL"));
    }

    [Fact]
    public void AzureAdOptions_WithValidInstance_IsValid()
    {
        // Arrange
        var options = new AzureAdOptions 
        { 
            Instance = "https://login.microsoftonline.com/"
        };

        // Act
        var results = ValidateModel(options);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void AzureAdOptions_WithInvalidInstance_IsInvalid()
    {
        // Arrange
        var options = new AzureAdOptions 
        { 
            Instance = "not-a-url"
        };

        // Act
        var results = ValidateModel(options);

        // Assert
        results.Should().Contain(r => r.ErrorMessage!.Contains("valid URL"));
    }

    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var ctx = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, ctx, validationResults, true);
        return validationResults;
    }
}
