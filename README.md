# 🚀 GithubUtility

> **Internal PR audit and release reporting utility** powered by Microsoft Agent Framework and GitHub Copilot SDK

---

## 📋 Table of Contents

- [Overview](#-overview)
- [Motivation & Use Cases](#-motivation--use-cases)
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

## 💡 Motivation & Use Cases

### Why GithubUtility?

As organizations scale their engineering teams and repositories, maintaining visibility into pull request activity, review processes, and release readiness becomes increasingly challenging. Manual tracking is time-consuming, error-prone, and doesn't scale. GithubUtility was built to solve these pain points by providing automated, intelligent insights into your GitHub workflow.

**The Problem:**
- 📊 **Lack of Visibility** - No centralized view of PR status across multiple repositories
- ⏱️ **Time-Consuming Audits** - Manual PR reviews and compliance checks take hours
- 🔍 **Inconsistent Reporting** - Different teams use different tools and formats
- 📈 **Scalability Issues** - Traditional methods break down with large teams
- 🎯 **Governance Gaps** - Difficult to enforce review policies and track compliance

### Practical Use Cases

#### 1. **Release Readiness Audits**
Before deploying to production, teams need to ensure all PRs are properly reviewed, approved, and tested. GithubUtility provides instant visibility into:
- Open PRs that might block a release
- PRs missing required approvals
- PRs with failing CI/CD checks
- Review activity and bottlenecks

**Example:** *"Show me all PRs in the release branch that are missing approvals from the security team"*

#### 2. **Compliance & Governance**
Organizations with strict compliance requirements (SOC 2, ISO 27001, HIPAA) need audit trails and evidence of code review processes. GithubUtility helps:
- Track review completion rates
- Identify PRs that bypassed required reviewers
- Generate audit reports for compliance reviews
- Monitor adherence to branch protection policies

**Example:** *"Generate a compliance report showing all PRs merged last quarter with their review status"*

#### 3. **Team Performance & Bottleneck Identification**
Engineering managers can use GithubUtility to:
- Identify teams with high PR backlogs
- Find reviewers who are overloaded
- Track average PR review times
- Discover process bottlenecks

**Example:** *"Which repositories have the longest average PR review time?"*

#### 4. **Cross-Organization Visibility**
For organizations with multiple teams, repositories, or business units:
- Centralized dashboard across all repositories
- Cross-team collaboration insights
- Organization-wide release coordination
- Unified reporting for leadership

**Example:** *"Show me all open PRs across all repositories in the 'platform' organization"*

#### 5. **Onboarding & Knowledge Sharing**
New team members can quickly understand:
- Active work streams and PRs
- Team review patterns and expectations
- Repository activity and health
- Historical context through natural language queries

**Example:** *"What PRs are currently being worked on by the backend team?"*

### Organizational Benefits

#### 🔒 **Auditing & Governance**
- **Automated Compliance Tracking** - Continuous monitoring of review policies and requirements
- **Audit Trail Generation** - Historical records of all PR activity for regulatory compliance
- **Policy Enforcement Visibility** - Identify violations of branch protection rules or review requirements
- **Risk Mitigation** - Early detection of PRs that don't meet organizational standards

#### 📊 **Data-Driven Decision Making**
- **Metrics & Analytics** - Quantify team performance, review efficiency, and release velocity
- **Trend Analysis** - Identify patterns in PR activity, review times, and bottlenecks
- **Resource Planning** - Data to inform staffing decisions and team structure
- **Process Improvement** - Evidence-based insights to optimize workflows

#### ⚡ **Operational Efficiency**
- **Time Savings** - Eliminate manual PR tracking and report generation
- **Reduced Context Switching** - Single interface for all PR-related queries
- **Natural Language Interface** - No need to learn complex query languages or APIs
- **Automated Reporting** - Scheduled reports for stakeholders and leadership

#### 🌐 **Cross-Team Collaboration**
- **Unified View** - Single source of truth across all repositories and teams
- **Improved Communication** - Clear visibility into what teams are working on
- **Release Coordination** - Better alignment for cross-team releases
- **Knowledge Sharing** - Easy discovery of relevant PRs and discussions

#### 🎯 **Scalability**
- **Multi-Repository Support** - Scale from single repo to hundreds of repositories
- **Enterprise Architecture** - Built for organizations of any size
- **Flexible Storage** - Choose the right database for your scale (SQLite to SQL Server)
- **Cloud-Ready** - Deploy to Azure with enterprise authentication and security

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
