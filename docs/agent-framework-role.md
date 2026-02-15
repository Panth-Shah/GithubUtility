# What Does Microsoft Agent Framework Do?

## Overview

The **Microsoft Agent Framework** provides the **orchestration and abstraction layer** that sits between your application and the underlying LLM service (GitHub Copilot SDK in this case).

## Division of Responsibilities

### GitHub Copilot SDK (`CopilotClient`)
**What it provides:**
- ✅ LLM service (the actual AI model)
- ✅ MCP server discovery and connection
- ✅ Authentication with GitHub Copilot service
- ✅ Low-level communication with Copilot backend

**What it does NOT provide:**
- ❌ High-level agent orchestration
- ❌ Session management
- ❌ Tool execution planning
- ❌ Multi-step workflow coordination

### Microsoft Agent Framework (`AIAgent`)
**What it provides:**
- ✅ **Agent Abstraction** - Consistent `AIAgent` interface regardless of underlying provider
- ✅ **Orchestration** - Coordinates LLM calls, tool execution, and response synthesis
- ✅ **Session Management** - `AgentSession` for conversation context
- ✅ **Planning** - Decides which tools to call based on user intent
- ✅ **Multi-Step Execution** - Chains multiple tool calls automatically
- ✅ **Response Synthesis** - Combines tool results into coherent answers
- ✅ **Safety Features** - Timeouts, validation, error handling
- ✅ **Provider Agnostic** - Same interface works with Copilot, Azure OpenAI, OpenAI, Anthropic, etc.

## How They Work Together

```
Your Application (PrAuditChatAgent)
    ↓
AIAgent (Agent Framework) ← Orchestration Layer
    ↓
CopilotClient (GitHub Copilot SDK) ← LLM Service
    ↓
GitHub Copilot Service (Actual LLM)
```

### Example Flow

1. **Your code calls:** `agent.RunAsync(session, options)`
   - This is the Agent Framework API

2. **Agent Framework:**
   - Analyzes the user query
   - Discovers available tools (via CopilotClient's MCP discovery)
   - Plans which tools to call
   - Coordinates execution

3. **CopilotClient:**
   - Receives planning requests from Agent Framework
   - Calls GitHub Copilot LLM service
   - Returns LLM responses back to Agent Framework

4. **Agent Framework:**
   - Executes tools based on LLM's plan
   - Feeds tool results back to LLM if more steps needed
   - Synthesizes final response
   - Returns to your code

## Why Use Agent Framework?

### 1. **Consistent Interface**
You can swap providers without changing your code:
```csharp
// Today: GitHub Copilot
var agent = copilotClient.AsAIAgent();

// Tomorrow: Azure OpenAI (same interface!)
var agent = azureOpenAIClient.AsAIAgent();
```

### 2. **Built-in Orchestration**
You don't need to manually:
- Parse LLM responses for tool calls
- Execute tools in sequence
- Feed results back to LLM
- Handle multi-step workflows
- Manage conversation context

### 3. **Safety & Reliability**
Framework provides:
- Timeout handling
- Error recovery
- Input validation
- Rate limiting
- Retry logic

### 4. **Multi-Agent Workflows**
Framework supports:
- Sequential workflows (agent A → agent B)
- Concurrent execution
- Agent handoffs
- Group chat scenarios

## In Your Code

```csharp
// CopilotClient = LLM provider
_copilotClient = new CopilotClient();

// AIAgent = Orchestration layer (Agent Framework)
_agent = _copilotClient.AsAIAgent(instructions: "...");

// Agent Framework handles:
// - Session management
// - Tool discovery (via CopilotClient)
// - Planning
// - Execution
// - Response synthesis
var session = await _agent.GetNewSessionAsync(cancellationToken);
var response = await _agent.RunAsync(session, null);
```

## Key Takeaway

**GitHub Copilot SDK** = The LLM engine (provides intelligence)  
**Microsoft Agent Framework** = The orchestration layer (provides structure and coordination)

Think of it like:
- **CopilotClient** = The car engine (power)
- **Agent Framework** = The transmission and steering (control and coordination)

You need both to drive! 🚗
