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
- **Package:** `Microsoft.Agents.AI.GitHub.Copilot`
- **Responsibilities:**
  - Connects to GitHub Copilot service
  - Automatically discovers MCP servers
  - Provides `AIAgent` interface for the Agent Framework

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

3. **Agent creates session** - New conversation session for this request

4. **Agent plans execution** - Framework analyzes intent and available tools

5. **Agent executes tools** - Calls MCP tools as needed (multi-step if required)

6. **Agent synthesizes response** - Combines tool results into natural language answer

7. **Response returned** - `ChatResponse` with answer and execution metadata

## Configuration

No special configuration required for the agent. MCP servers are automatically discovered by the GitHub Copilot SDK.

**Important:** The `GitHubConnector:Mcp` configuration in `appsettings.json` is used by the **ingestion worker** (`McpGitHubDataSource`), NOT by the chat agent. The chat agent uses Copilot SDK's automatic MCP server discovery.

The agent uses:
- Agent instructions are hardcoded in `PrAuditChatAgent` constructor
- MCP servers are discovered automatically by Copilot SDK (no manual configuration needed)

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
