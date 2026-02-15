# GitHub PR Audit Utility - Implementation Plan

## 1. Goal
Build an internal-hosted utility using GitHub Copilot SDK + Microsoft Agent Framework that periodically collects and reports, also provides user an ability to chat using copilot with Github MCP Server for multi turn conversation:
- Open PR inventory
- Published PR stats (merged/closed over time)
- Contributor and reviewer activity stats
- Audit-oriented release insights

## 2. Scope (MVP)
### In scope
- Scheduled data collection from GitHub
- PR + review + merge metadata normalization
- Historical snapshots for auditability
- Report generation (CSV/JSON + basic dashboard/API)
- Internal-only hosting and authentication
- Build chat like interface
- Host the agent with azure infrastructure and secure it

### Out of scope (MVP)
- Mutating GitHub resources (no approve/merge/comment)
- Advanced forecasting/ML scoring
- Enterprise BI replacement

## 3. Target Architecture
- **Agent runtime:** ✅ **Implemented** - Microsoft Agent Framework with GitHub Copilot SDK integration
  - Uses `CopilotClient` and `AIAgent` for natural language processing
  - Automatic MCP server discovery and tool execution
  - Multi-step planning and execution handled by the framework
- Data access: GitHub MCP server (preferred) with sample fallback for local development
- Scheduler: internal cron/Azure Function timer/Container Apps job
- Storage:
  - Relational DB for normalized facts (PostgreSQL/SQL Server)
  - Optional blob storage for raw payload snapshots
- Reporting surface:
  - Internal API endpoints
  - Export jobs (daily CSV)
  - Optional lightweight web UI

## 4. Core Data Model
### Entities
- `repositories`
- `pull_requests`
- `pull_request_events`
- `pull_request_reviews`
- `users`
- `audit_snapshots`
- `release_windows`

### Key metrics
- Open PR count by repo/team/age
- Merged PR count by day/week/release
- Time to merge (created -> merged)
- Approval latency and reviewer load
- Merge compliance signals (approval present, branch policy alignment)

## 5. Agent Design
### Primary agent: `PrAuditChatAgent` ✅ **Implemented**
- Uses **GitHub Copilot SDK** with **Microsoft Agent Framework**
- Responsibilities:
  - Receives natural language user queries
  - Automatically discovers available MCP tools
  - Plans and executes multi-step tool calls based on user intent
  - Synthesizes responses from tool execution results
  - Handles conversation context via sessions

### MCP Tools Available (via GitHub MCP Server)
- `list_repositories` - List all repositories in organization
- `list_pull_requests` - List PRs with filtering options
- `list_reviews` - Get reviews for a specific PR
- `list_pull_request_events` - Get events for a specific PR

**Note:** The agent automatically discovers and uses these tools - no manual tool registration required.

## 6. Execution Workflows
### Scheduled ingestion (every 1h default)
1. Load last successful cursor per repository.
2. Fetch changed PRs in window.
3. Fetch reviews/events for affected PRs.
4. Upsert normalized records + append raw snapshot.
5. Recompute aggregate metrics.
6. Emit run summary and alert on failures/gaps.

### On-demand reporting
- Query by repo, team, date range, release tag/branch.
- Return summary + drill-down rows.

## 7. Security and Compliance
- MCP credentials scoped read-only where supported.
- Secrets stored in internal secret manager (Key Vault/Vault).
- Internal auth for dashboard/API (AAD/SSO).
- Audit log for each ingestion run and report request.
- PII minimization and retention policy enforcement.

## 8. Delivery Phases
### Phase 0 - Foundations (1-2 days)
- Confirm repo scope, orgs, and required metrics.
- Choose runtime stack (.NET recommended for Copilot SDK + Agent Framework parity).
- Decide hosting target and database.

### Phase 1 - Ingestion MVP (3-5 days)
- Scaffold service and `PrAuditAgent`.
- Connect GitHub MCP server and validate tool surface.
- Implement incremental ingestion + persistence.
- Add run telemetry and retries.

### Phase 2 - Reporting MVP (3-4 days)
- Implement aggregate metric jobs.
- Expose internal API + CSV export.
- Add baseline dashboard/cards for auditing and release ops.

### Phase 3 - Hardening (2-4 days)
- Add data quality checks and backfill command.
- Add alerting (run failure, stale cursor, API throttling).
- Performance tuning and retention jobs.

## 9. Definition of Done (MVP)
- Hourly ingestion runs reliably for target repositories.
- Open/merged/reviewer/user metrics available for any date range in last N months.
- Release managers can export audit reports without manual GitHub scraping.
- Runbook + deployment docs completed.

## 10. Immediate Next Tasks
1. ✅ **Completed:** Tech stack confirmed and implemented: `.NET 8 + Agent Framework + GitHub Copilot SDK`
2. Confirm storage: `PostgreSQL` vs `SQL Server`.
3. Confirm scheduling cadence and SLA.
4. ✅ **Completed:** Project structure scaffolded and config templates created
5. Verify MCP server connection and tool execution flow
6. Test chat agent with real user queries
7. Add streaming response support for better UX
8. Implement first end-to-end run on 1 pilot repository
