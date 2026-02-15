using System.ComponentModel.DataAnnotations;

namespace GithubUtility.App.Options;

public sealed class AuditStoreOptions
{
    public const string SectionName = "AuditStore";

    [Required(ErrorMessage = "Provider is required")]
    [RegularExpression("^(Sqlite|SqlServer|Postgres)$", ErrorMessage = "Provider must be Sqlite, SqlServer, or Postgres")]
    public string Provider { get; init; } = "Sqlite";

    [Required(ErrorMessage = "ConnectionString is required")]
    [MinLength(10, ErrorMessage = "ConnectionString must be at least 10 characters")]
    public string ConnectionString { get; init; } = "Data Source=./data/audit.db";

    public bool InitializeSchemaOnStartup { get; init; } = true;
}
