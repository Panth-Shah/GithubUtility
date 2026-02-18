# Chat Agent Architecture

## Overview

The chat agent (`PrAuditChatAgent`) uses **GitHub Copilot SDK** integrated with **Microsoft Agent Framework** to provide intelligent, natural language interactions for GitHub PR auditing.

## Architecture

```
User Query
    ↓
PrAuditChatAgent
    ↓
CopilotClient → AIAgent (Microsoft Agent Framework)
    ↓
Automatic MCP Server Discovery
    ↓
Tool Schema Discovery
    ↓
Multi-Step Planning & Execution
    ↓
Response Synthesis
    ↓
ChatResponse
```

## Key Components

### PrAuditChatAgent
- **Location:** `src/GithubUtility.App/Agents/PrAuditChatAgent.cs`
- **Responsibilities:**
  - Receives `ChatRequest` with user query and optional filters
  - Builds user intent from request parameters
  - Manages `CopilotClient` lifecycle
  - Creates and manages agent sessions
  - Returns `ChatResponse` with agent's answer

### CopilotClient
- **Package:** `GitHub.Copilot.SDK` (transitively included by `Microsoft.Agents.AI.GitHub.Copilot`)
- **How it works:**
  - Wraps the **`@github/copilot` npm CLI binary** as a child process
  - At startup it calls `CopilotClientOptions.CliPath` (default: `"copilot"` on PATH) to launch the CLI server
  - Communicates over stdio (default) or TCP
  - The CLI authenticates with GitHub using the `GITHUB_TOKEN` environment variable, which `CopilotClientOptions.Environment` forwards from the host into the subprocess
  - Automatically discovers MCP servers and provides the `AIAgent` interface for the Agent Framework
- **Deployment note:** Because the CLI is an npm package, the Docker image installs **Node.js 22 LTS** and `@github/copilot` at build time — no separate installation is needed on the Azure Container Apps host

### AIAgent (Microsoft Agent Framework)
- **Package:** `Microsoft.Agents.AI`
- **Responsibilities:**
  - **Orchestration Layer** - Coordinates between LLM (via CopilotClient) and tools
  - **Session Management** - Manages conversation context via `AgentSession`
  - **Planning** - Decides which tools to call and in what order
  - **Multi-Step Execution** - Chains multiple tool calls to answer complex queries
  - **Response Synthesis** - Combines tool results into natural language responses
  - **Safety & Guards** - Provides timeouts, validation, error handling
  - **Consistent Interface** - Same `AIAgent` interface works with different providers (Copilot, Azure OpenAI, OpenAI, etc.)

## MCP Integration

The GitHub Copilot SDK automatically:
1. **Discovers MCP servers** - No manual configuration required
2. **Retrieves tool schemas** - Understands available tools and parameters
3. **Executes tools** - Calls MCP tools based on agent's plan
4. **Handles results** - Processes tool outputs and synthesizes responses

### Available MCP Tools

The agent can use any tools exposed by connected MCP servers. For GitHub operations:
- `list_repositories` - List repositories in organization
- `list_pull_requests` - Query PRs with filters (repository, date range, state)
- `list_reviews` - Get PR reviews
- `list_pull_request_events` - Get PR timeline events

## Request Flow

1. **User sends query** via `POST /api/chat/query`
   ```json
   {
     "query": "Show me open PRs older than 30 days",
     "repository": "org/repo",
     "from": "2024-01-01",
     "to": "2024-01-31"
   }
   ```

2. **Agent builds intent** - Combines query with context (dates, repository)

3. **Agent creates session** - New conversation session for this request via `_agent.GetNewSessionAsync()`

4. **Agent runs with message** - `_agent.RunAsync(userIntent, session, options, ct)` sends the user message and triggers planning

5. **Agent executes tools** - Calls MCP tools as needed (multi-step if required)

6. **Agent synthesizes response** - Combines tool results into natural language answer

7. **Response returned** - `ChatResponse` with answer and execution metadata

## Configuration

The agent is configured via the `Copilot` section in `appsettings.json`, bound to `CopilotOptions`:

```json
{
  "Copilot": {
    "CliPath": "copilot",
    "CliUrl": "",
    "GitHubTokenEnvVar": "GITHUB_TOKEN",
    "Model": "gpt-4o",
    "SystemPrompt": "You are a helpful assistant for GitHub PR auditing..."
  }
}
```

| Property | Description |
|----------|-------------|
| `CliPath` | Path to the `@github/copilot` binary. Defaults to `"copilot"` (resolved from PATH). In Docker this is already on PATH after `npm install -g @github/copilot`. |
| `CliUrl` | Optional URL of an already-running CLI server (e.g. sidecar container). When set, no process is spawned. |
| `GitHubTokenEnvVar` | Name of the env var holding the GitHub PAT. The value is read from the host env at startup and injected into the CLI subprocess via `CopilotClientOptions.Environment`. |
| `Model` | LLM model passed to each `SessionConfig` (e.g. `"gpt-4o"`, `"claude-sonnet-4-5"`). |
| `SystemPrompt` | System message appended to every Copilot session. |

**Important:** The `GitHubConnector:Mcp` section in `appsettings.json` is used exclusively by the **ingestion worker** (`McpGitHubDataSource`). The chat agent uses the Copilot SDK's own automatic MCP server discovery — those are two separate mechanisms.

See `docs/mcp-tool-integration.md` for details on MCP tool discovery vs manual tool registration.

## Benefits

1. **Automatic Tool Discovery** - No manual tool registration
2. **Intelligent Planning** - Agent decides which tools to call and in what order
3. **Multi-Step Execution** - Can chain multiple tool calls to answer complex queries
4. **Natural Language** - Users can ask questions in plain English
5. **Built-in Guards** - Framework provides safety features (timeouts, validation, etc.)

## Example Queries

- "What are the open PRs in the platform-service repository?"
- "Show me PRs merged in the last week"
- "Which PRs don't have approvals?"
- "List all repositories and their open PR counts"
- "What reviews were submitted for PR #123?"

## Future Enhancements

- [ ] Streaming responses for real-time updates
- [ ] Multi-turn conversation support (session persistence)
- [ ] Custom tool functions beyond MCP tools
- [ ] Response formatting (tables, charts, etc.)
- [ ] Query validation and error handling improvements
