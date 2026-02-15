# GithubUtility Technical Design Specification

## 1. Purpose
GithubUtility is an internal PR audit and release-management utility built on .NET 8. It ingests PR data from GitHub through either:
- GitHub MCP server tool connector, or
- Sample data source for local development.

It stores historical snapshots in persistent storage, computes audit/release metrics, and exposes both API and chat-driven reporting interfaces.

## 2. Design Goals
- Internal-only deployment with enterprise auth controls.
- Reliable scheduled ingestion with incremental cursor sync.
- Auditable historical state of PR metadata and review outcomes.
- Pluggable data access (MCP tools vs sample data).
- Chat entry point using GitHub Copilot SDK with Microsoft Agent Framework for intelligent tool orchestration.

## 3. High-Level Architecture
- `GithubUtility.App` (ASP.NET Core host)
  - Background worker: periodic ingestion scheduler.
  - Minimal API: ingestion + reports + chat query.
  - Connectors: MCP tool client and sample data source.
  - Agent layer: PR audit chat agent using GitHub Copilot SDK with Microsoft Agent Framework.
- `GithubUtility.Core` (domain and orchestration)
  - `PrAuditOrchestrator` and domain models.
  - Interfaces for GitHub data source and audit repository.

## 4. Component Design
### 4.1 Ingestion Orchestrator
`IPrAuditOrchestrator` / `PrAuditOrchestrator`
- Gets repositories from `IGitHubDataSource`.
- Loads per-repo cursor from `IAuditRepository`.
- Fetches changed PRs since cursor.
- Upserts snapshots and updates cursor.
- Generates ingestion run result with error collection.

### 4.2 GitHub Connectors
`IGitHubDataSource` implementations:
- `McpGitHubDataSource`
  - Calls MCP tools through `IMcpToolClient`.
  - Tool names and endpoint are configurable.
- `SampleGitHubDataSource`
  - Local deterministic data for development.

### 4.3 MCP Tool Bridge
`IMcpToolClient` / `McpHttpToolClient`
- Generic HTTP POST invocation for tool execution.
- Supports optional bearer API key.
- Expected response envelope is JSON and parsed by connector.

### 4.4 Persistence Layer
`IAuditRepository` implementations:
- `SqlAuditRepository`
  - Provider modes: `Sqlite`, `SqlServer`, `Postgres`.
  - Schema auto-init support.
  - Upsert support with provider-specific SQL.
- `JsonAuditRepository`
  - File fallback mode (`AuditStore:Provider = Json`).

Stored entities:
- `repository_cursors`
- `pull_request_snapshots`
  - includes normalized fields and serialized `reviews_json` / `events_json`

### 4.5 Chat Agent Layer
- `IPrAuditChatAgent` / `PrAuditChatAgent`
  - Uses **GitHub Copilot SDK** with **Microsoft Agent Framework**
  - `CopilotClient` and `AIAgent` for natural language processing
  - Automatically discovers and connects to MCP servers
  - Handles multi-step tool execution and planning
  - Provides natural language responses based on user queries

## 5. API Surface
- `POST /api/ingestion/run`
- `GET /api/reports/open-prs`
- `GET /api/reports/user-stats`
- `GET /api/reports/release-summary`
- `GET /api/reports/repositories`
- `POST /api/chat/query`

## 6. Configuration Model
- `Scheduler`
  - `IngestionIntervalMinutes`
- `AuditStore`
  - `Provider` (`Json|Sqlite|SqlServer|Postgres`)
  - `ConnectionString`
  - `InitializeSchemaOnStartup`
- `GitHubConnector`
  - `Mode` (`Sample|Mcp`)
  - `Organization`, `Repositories[]`
  - `Mcp` subsection (endpoint, tool names)
  
**Note:** The chat agent uses GitHub Copilot SDK which automatically discovers MCP servers. No separate Copilot SDK configuration is required.

## 7. Security Design
- MCP API key is optional and supported via bearer token.
- No mutation operations are implemented against GitHub.
- Utility is intended for private network deployment behind org SSO.

## 8. Operational Flow
1. Worker trigger starts ingestion.
2. Orchestrator syncs repository-by-repository using cursor windows.
3. Data source fetches PR/review/event data.
4. Repository upserts snapshots and updates cursors.
5. Reports and chat endpoints read computed views from stored snapshots.

## 9. Observability and Error Handling
- Background ingestion logs run counts and failures.
- Partial failures are captured per repository and returned in run result.
- Connector failures do not crash service process; they surface as run errors.

## 10. Known Gaps / Next Steps
- ✅ **Completed:** Integrated GitHub Copilot SDK with Microsoft Agent Framework for chat agent
- Add identity and authorization middleware for enterprise auth.
- Add automated tests and migration framework for SQL schema evolution.
- Verify MCP server auto-discovery and tool execution flow
- Add streaming response support for chat endpoint