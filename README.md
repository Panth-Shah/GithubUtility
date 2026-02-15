# GithubUtility

Internal PR audit and release reporting utility.

## Stack
- .NET 8
- ASP.NET Core minimal APIs
- Background ingestion worker
- Microsoft Agent Framework with GitHub Copilot SDK integration
- Pluggable connectors: MCP tool bridge or sample data source
- Pluggable storage: JSON or SQL repository

## Run
```powershell
dotnet run --project src/GithubUtility.App/GithubUtility.App.csproj
```

## Key Endpoints
- `POST /api/ingestion/run`
- `GET /api/reports/open-prs`
- `GET /api/reports/user-stats`
- `GET /api/reports/release-summary`
- `GET /api/reports/repositories`
- `POST /api/chat/query`

## Configuration
`src/GithubUtility.App/appsettings.json`
- `GitHubConnector:Mode` = `Sample | Mcp`
- `AuditStore:Provider` = `Json | Sqlite | SqlServer | Postgres`
- `GitHubConnector:Mcp` - MCP server endpoint and tool configuration

## Chat Agent
The chat agent (`POST /api/chat/query`) uses **GitHub Copilot SDK** with **Microsoft Agent Framework** to:
- Automatically discover and connect to MCP servers
- Plan and execute multi-step tool calls based on user intent
- Provide natural language responses about PRs, repositories, and reviews

The agent leverages the Agent Framework's built-in capabilities for:
- Multi-step planning and tool execution
- Automatic MCP server discovery
- Tool schema discovery
- Built-in guards and safety features

## Database Scripts
- `db/migrations/V1__init_sqlite.sql`
- `db/migrations/V1__init_postgres.sql`
- `db/migrations/V1__init_sqlserver.sql`

## Documentation
- Plan: `plan.md`
- Technical design spec: `docs/technical-design-spec.md`
- Agent architecture: `docs/agent-architecture.md`
- Agent Framework role: `docs/agent-framework-role.md` - Explains what Microsoft Agent Framework does vs GitHub Copilot SDK
- MCP tool integration: `docs/mcp-tool-integration.md` - Explains automatic MCP tool discovery vs manual registration
- **Deployment Guide**: `docs/deployment-guide.md` - Comprehensive Azure deployment guide with SSO
- **Quick Start Deployment**: `README-DEPLOYMENT.md` - Quick deployment steps
- **GitHub SSO Integration**: `docs/github-sso-integration.md` - Configure GitHub SSO with Azure AD

## Deployment

### Quick Start
See `README-DEPLOYMENT.md` for quick deployment steps.

### Full Deployment Guide
See `docs/deployment-guide.md` for comprehensive Azure deployment instructions with:
- Microsoft Entra ID (Azure AD) authentication
- GitHub SSO integration
- Azure Container Apps deployment
- Infrastructure as Code (Bicep)
- CI/CD with GitHub Actions

### Authentication
The application uses **Microsoft Entra ID (Azure AD)** for authentication, which can be integrated with GitHub SSO for seamless credential management. See `docs/github-sso-integration.md` for SSO setup.

## Notes
- SQL providers require corresponding ADO.NET provider registration in runtime environment.
- MCP tool names are configurable under `GitHubConnector:Mcp`.
- Authentication is optional in development mode (when Azure AD is not configured).