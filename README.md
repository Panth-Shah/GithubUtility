# 🚀 GithubUtility

> **Internal PR audit and release reporting utility** powered by Microsoft Agent Framework and GitHub Copilot SDK

---

## 📋 Table of Contents

- [Overview](#-overview)
- [Tech Stack](#-tech-stack)
- [Quick Start](#-quick-start)
- [API Endpoints](#-api-endpoints)
- [Chat Agent](#-chat-agent)
- [Configuration](#-configuration)
- [Deployment](#-deployment)
- [Documentation](#-documentation)
- [Database](#-database)

---

## 🎯 Overview

GithubUtility is an intelligent PR auditing system that helps teams track pull requests, review activity, and generate release reports. It features an AI-powered chat agent that can answer natural language queries about your repositories.

**Key Features:**
- 🤖 AI-powered chat agent with natural language understanding
- 📊 Automated PR tracking and auditing
- 📈 Comprehensive reporting (open PRs, user stats, releases)
- 🔌 Pluggable architecture for connectors and storage
- 🔒 Enterprise-ready authentication with Azure AD
- 🌐 MCP (Model Context Protocol) integration

---

## 🛠️ Tech Stack

| Component | Technology |
|-----------|-----------|
| **Runtime** | .NET 8 |
| **API Framework** | ASP.NET Core Minimal APIs |
| **Background Processing** | Hosted Services / Workers |
| **AI Agent** | Microsoft Agent Framework + GitHub Copilot SDK |
| **Data Connectors** | MCP Tool Bridge / Sample Data |
| **Storage Options** | JSON / SQLite / SQL Server / PostgreSQL |

---

## 🚀 Quick Start

### Prerequisites
- .NET 8 SDK
- (Optional) SQL database for production use

### Running Locally

```powershell
# Clone the repository
git clone https://github.com/Panth-Shah/GithubUtility.git
cd GithubUtility

# Run the application
dotnet run --project src/GithubUtility.App/GithubUtility.App.csproj
```

The API will be available at `https://localhost:5001` (or configured port).

### First Steps
1. Configure your data source in [`appsettings.json`](src/GithubUtility.App/appsettings.json)
2. Choose your storage provider (JSON for dev, SQL for production)
3. Run ingestion: `POST /api/ingestion/run`
4. Query your data via API endpoints or chat agent

---

## 🌐 API Endpoints

### Data Ingestion
- **`POST /api/ingestion/run`** - Trigger manual data ingestion from GitHub

### Reports & Analytics
- **`GET /api/reports/open-prs`** - Get all open pull requests
- **`GET /api/reports/user-stats`** - View user contribution statistics
- **`GET /api/reports/release-summary`** - Generate release summary reports
- **`GET /api/reports/repositories`** - List tracked repositories

### AI Chat Agent
- **`POST /api/chat/query`** - Ask questions in natural language about your PRs

**Example Chat Queries:**
```json
{
  "message": "Show me all open PRs for user john.doe"
}
```
```json
{
  "message": "What's the review status for PR #123?"
}
```

---

## 🤖 Chat Agent

The chat agent leverages **GitHub Copilot SDK** with **Microsoft Agent Framework** to provide intelligent, context-aware responses about your repositories.

### Capabilities
✅ **Multi-step Planning** - Breaks down complex queries into actionable steps  
✅ **Automatic Tool Discovery** - Finds and connects to MCP servers dynamically  
✅ **Natural Language Understanding** - Interprets user intent without rigid commands  
✅ **Built-in Safety Guards** - Validates and sanitizes tool executions  

### How It Works
1. User sends a natural language query
2. Agent Framework plans the execution strategy
3. Agent discovers and invokes required tools (MCP or local)
4. Results are synthesized into a human-readable response

📖 **Learn More:** [Agent Architecture](docs/agent-architecture.md) | [Agent Framework Role](docs/agent-framework-role.md)

---

## ⚙️ Configuration

Configuration is managed through [`appsettings.json`](src/GithubUtility.App/appsettings.json).

### Key Settings

```json
{
  "GitHubConnector": {
    "Mode": "Sample | Mcp",  // Choose data source
    "Mcp": {
      // MCP server configuration
    }
  },
  "AuditStore": {
    "Provider": "Json | Sqlite | SqlServer | Postgres"
  }
}
```

### Configuration Options

| Setting | Options | Description |
|---------|---------|-------------|
| `GitHubConnector:Mode` | `Sample` / `Mcp` | Data source: sample data or MCP server |
| `AuditStore:Provider` | `Json` / `Sqlite` / `SqlServer` / `Postgres` | Storage backend |
| `Scheduler:Enabled` | `true` / `false` | Enable automatic ingestion |

📖 **Learn More:** [MCP Tool Integration](docs/mcp-tool-integration.md)

---

## 🚢 Deployment

### Quick Deploy to Azure

```bash
# Use the quick deployment script
./scripts/deploy-azure.sh
```

**📚 Deployment Guides:**
- 🏃 **[Quick Start Deployment](README-DEPLOYMENT.md)** - Get up and running in minutes
- 📘 **[Comprehensive Deployment Guide](docs/deployment-guide.md)** - Full Azure setup with all features
- 🔐 **[GitHub SSO Integration](docs/github-sso-integration.md)** - Configure single sign-on

### Deployment Features
- ☁️ Azure Container Apps
- 🔐 Microsoft Entra ID (Azure AD) authentication
- 🔄 CI/CD with GitHub Actions
- 🏗️ Infrastructure as Code (Bicep templates)
- 🔗 GitHub SSO integration

### Infrastructure

The [`infrastructure/`](infrastructure/) folder contains:
- **`main.bicep`** - Azure infrastructure definitions
- Automated resource provisioning
- Network and security configuration

---

## 📚 Documentation

### Core Documentation

| Document | Description |
|----------|-------------|
| 📋 **[Project Plan](plan.md)** | Development roadmap and milestones |
| 🏗️ **[Technical Design Spec](docs/technical-design-spec.md)** | System architecture and design decisions |
| 🤖 **[Agent Architecture](docs/agent-architecture.md)** | AI agent design and implementation |
| 🧩 **[Agent Framework Role](docs/agent-framework-role.md)** | Microsoft Agent Framework vs GitHub Copilot SDK |
| 🔌 **[MCP Tool Integration](docs/mcp-tool-integration.md)** | Automatic tool discovery vs manual registration |

### Deployment Documentation

| Document | Description |
|----------|-------------|
| 🏃 **[Quick Start Deployment](README-DEPLOYMENT.md)** | Fast deployment steps |
| 📘 **[Comprehensive Deployment Guide](docs/deployment-guide.md)** | Full Azure deployment with SSO |
| 🔐 **[GitHub SSO Integration](docs/github-sso-integration.md)** | Configure GitHub authentication |
| 📊 **[Deployment Summary](docs/deployment-summary.md)** | Overview of deployment options |

---

## 🗄️ Database

### Supported Databases
- **JSON** - File-based storage for development
- **SQLite** - Lightweight embedded database
- **SQL Server** - Enterprise SQL database
- **PostgreSQL** - Open-source SQL database

### Migration Scripts

All database schemas are versioned and available in [`db/migrations/`](db/migrations/):

- [`V1__init_sqlite.sql`](db/migrations/V1__init_sqlite.sql) - SQLite initialization
- [`V1__init_postgres.sql`](db/migrations/V1__init_postgres.sql) - PostgreSQL initialization
- [`V1__init_sqlserver.sql`](db/migrations/V1__init_sqlserver.sql) - SQL Server initialization

---

## 🔧 Development

### Project Structure

```
GithubUtility/
├── src/
│   ├── GithubUtility.App/      # Main application & API
│   └── GithubUtility.Core/     # Core business logic
├── tests/                       # Unit and integration tests
├── infrastructure/              # Bicep/ARM templates
├── db/migrations/               # Database schemas
├── docs/                        # Documentation
└── scripts/                     # Deployment scripts
```

### Running Tests

```powershell
dotnet test
```

---

## 📝 Notes

- 🔌 SQL providers require corresponding ADO.NET provider registration
- 🛠️ MCP tool names are fully configurable under `GitHubConnector:Mcp`
- 🔓 Authentication is optional in development mode (when Azure AD is not configured)
- 📦 The application automatically creates required database tables on first run

---

## 🤝 Contributing

This is an internal utility, but contributions are welcome! Please ensure:
- Code follows existing patterns
- Tests are included for new features
- Documentation is updated

---

## 📄 License

Internal use only.

---

**Built with ❤️ using Microsoft Agent Framework and GitHub Copilot SDK**
