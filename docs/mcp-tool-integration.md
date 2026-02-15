# MCP Tool Integration with GitHub Copilot SDK

## Short Answer

**No, you do NOT need to manually provide/register MCP tools.** The GitHub Copilot SDK automatically discovers MCP servers and their tools when it connects.

## How It Works

### Automatic Discovery

When `CopilotClient` starts and connects to GitHub Copilot service:

1. **MCP Server Discovery** - SDK automatically discovers configured MCP servers
2. **Tool Schema Retrieval** - SDK queries each MCP server for available tools
3. **Tool Registration** - SDK automatically registers all discovered tools with the agent
4. **Ready to Use** - Agent can immediately use any tool from connected MCP servers

### Your Current Setup

```csharp
// This is all you need!
_copilotClient = new CopilotClient();
_agent = _copilotClient.AsAIAgent(
    instructions: "You are a helpful assistant..."
);

// When CopilotClient.StartAsync() is called:
// ✅ Automatically discovers MCP servers
// ✅ Automatically retrieves tool schemas
// ✅ Automatically makes tools available to agent
// ❌ NO manual tool registration needed
```

## What About `IMcpToolClient`?

You might notice you have `IMcpToolClient` in your codebase. This is used for:

- **Scheduled Ingestion Worker** (`McpGitHubDataSource`) - Direct MCP tool calls for data ingestion
- **NOT for the Chat Agent** - The chat agent uses Copilot SDK's automatic discovery

These are two different use cases:

### 1. Direct MCP Tool Calls (Ingestion Worker)
```csharp
// Used by PrAuditOrchestrator for scheduled data collection
var response = await mcpToolClient.InvokeToolAsync(
    "list_pull_requests",
    new { repository = "org/repo", since = "2024-01-01" },
    cancellationToken);
```
- **Purpose:** Scheduled background jobs
- **Approach:** Direct HTTP calls to MCP server
- **Manual:** You specify tool names and parameters

### 2. Agent-Based Tool Calls (Chat Agent)
```csharp
// Used by PrAuditChatAgent for natural language queries
var response = await _agent.RunAsync(session, null);
// Agent automatically:
// - Discovers tools from MCP servers
// - Plans which tools to call
// - Executes tools
// - Synthesizes response
```
- **Purpose:** Natural language interactions
- **Approach:** Agent Framework orchestration
- **Automatic:** SDK discovers and uses tools automatically

## Can You Add Custom Tools?

Yes! You can provide **additional** custom tools alongside MCP-discovered tools:

```csharp
// Custom function tool
AIFunction customTool = AIFunctionFactory.Create(
    (string input) => { return ProcessData(input); },
    "ProcessData",
    "Processes input data"
);

// Agent gets BOTH:
// 1. All tools from MCP servers (automatic)
// 2. Your custom tools (explicit)
_agent = _copilotClient.AsAIAgent(
    tools: [customTool],  // Your custom tools
    instructions: "..."
);
```

## Configuration

### MCP Server Configuration

MCP servers are typically configured in:
- **VS Code settings** (if using VS Code)
- **Environment variables**
- **GitHub Copilot CLI configuration**

The SDK reads these configurations automatically - you don't need to configure them in your app.

### Your `appsettings.json` MCP Config

The `GitHubConnector:Mcp` section in your `appsettings.json` is used by:
- ✅ `McpGitHubDataSource` (ingestion worker)
- ❌ NOT by `PrAuditChatAgent` (uses SDK's auto-discovery)

```json
{
  "GitHubConnector": {
    "Mcp": {
      "Endpoint": "http://localhost:8080/tools/invoke",
      "ListRepositoriesTool": "list_repositories"
      // ↑ Used by ingestion worker, not chat agent
    }
  }
}
```

## Summary

| Aspect | Ingestion Worker | Chat Agent |
|--------|-----------------|------------|
| **MCP Integration** | Manual via `IMcpToolClient` | Automatic via Copilot SDK |
| **Tool Discovery** | You specify tool names | SDK discovers automatically |
| **Tool Registration** | Direct HTTP calls | SDK handles registration |
| **Use Case** | Scheduled data collection | Natural language queries |

## Key Takeaway

**For the Chat Agent:**
- ✅ Copilot SDK automatically discovers MCP servers
- ✅ Copilot SDK automatically discovers tools
- ✅ No manual tool registration needed
- ✅ Just create the agent and it works!

**For the Ingestion Worker:**
- Uses `IMcpToolClient` for direct calls
- Manual tool invocation
- Different use case (scheduled jobs vs interactive chat)
