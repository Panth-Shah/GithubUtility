using System.ComponentModel.DataAnnotations;

namespace GithubUtility.App.Options;

public sealed class AzureAdOptions
{
    public const string SectionName = "AzureAd";

    [Required]
    [Url(ErrorMessage = "Instance must be a valid URL")]
    public string Instance { get; init; } = "https://login.microsoftonline.com/";

    public string? TenantId { get; init; }

    public string? ClientId { get; init; }

    public string? ClientSecret { get; init; }

    [Required]
    public string CallbackPath { get; init; } = "/.auth/login/aad/callback";

    [Required]
    public string SignedOutCallbackPath { get; init; } = "/.auth/logout/aad/callback";

    [Required]
    public string[] Scopes { get; init; } = { "User.Read", "email" };
}
