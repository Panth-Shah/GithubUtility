# Quick Start Deployment Guide

## Prerequisites

1. **Azure CLI** installed and configured
2. **Azure Subscription** with appropriate permissions
3. **GitHub Personal Access Token** (fine-grained) with:
   - **Copilot Requests** (read + write) — required by the GitHub Copilot CLI inside the container
   - `repo` read — required by the MCP ingestion connector
4. **Docker** installed locally (for building and testing the image)
5. **Azure AD App Registration** created (see detailed guide)

> **About the Docker image:** The `Dockerfile` installs Node.js 22 LTS and the `@github/copilot` npm package at build time. The `GITHUB_TOKEN` env var is forwarded into the Copilot CLI subprocess at runtime — never baked into the image.

## Quick Deployment Steps

### 1. Create Azure AD App Registration

```bash
# Login to Azure
az login

# Create App Registration
az ad app create --display-name "GitHub Utility"

# Get App ID
APP_ID=$(az ad app list --display-name "GitHub Utility" --query [0].appId -o tsv)
echo "App ID: $APP_ID"

# Create Service Principal
az ad sp create --id $APP_ID

# Create Client Secret
CLIENT_SECRET=$(az ad app credential reset --id $APP_ID --query password -o tsv)
echo "Client Secret: $CLIENT_SECRET"
```

**Important:** Save the App ID and Client Secret - you'll need them for deployment.

### 2. Configure App Registration Redirect URI

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to **Microsoft Entra ID** → **App registrations** → **GitHub Utility**
3. Click **Authentication**
4. Add **Web** platform
5. Add redirect URI: `https://<your-container-app-url>/.auth/login/aad/callback`
   - You'll get the URL after deployment, or use a placeholder for now
6. Enable **ID tokens** and **Access tokens**
7. Click **Save**

### 3. Run Deployment Script

**For Linux/Mac:**
```bash
chmod +x scripts/deploy-azure.sh
./scripts/deploy-azure.sh
```

**For Windows (PowerShell):**
```powershell
.\scripts\deploy-azure.ps1
```

The script will prompt you for:
- Azure AD Client ID (from step 1)
- SQL Server admin username and password
- GitHub MCP API Key (Personal Access Token)

### 4. Update Redirect URI

After deployment, update the redirect URI in Azure AD with the actual Container App URL:

```bash
# Get Container App URL
CONTAINER_APP_URL=$(az containerapp show \
  --name ca-githubutility-prod \
  --resource-group rg-githubutility-prod \
  --query properties.configuration.ingress.fqdn -o tsv)

echo "Update redirect URI to: https://$CONTAINER_APP_URL/.auth/login/aad/callback"
```

### 5. Configure GitHub SSO (Optional)

If you want to use GitHub SSO for authentication:

1. In Azure AD, create Enterprise Application
2. Configure SAML SSO
3. Configure GitHub Organization SAML SSO
4. Link the two

See `docs/deployment-guide.md` for detailed SSO configuration.

## Manual Deployment (Alternative)

If you prefer manual deployment:

### 1. Deploy Infrastructure

```bash
az deployment group create \
  --resource-group rg-githubutility-prod \
  --template-file infrastructure/main.bicep \
  --parameters \
    appName=githubutility \
    environment=prod \
    tenantId=<your-tenant-id> \
    clientId=<your-client-id> \
    sqlAdminUsername=<sql-admin> \
    sqlAdminPassword=<sql-password> \
    githubMcpApiKey=<github-pat-with-repo-read> \
    githubCopilotToken=<github-pat-with-copilot-requests> \
    copilotModel=gpt-4o
```

### 2. Store the GitHub Copilot Token in Key Vault

The container requires a GitHub PAT with **Copilot Requests** permission. Store it as a Key Vault secret so it can be injected as an environment variable at runtime — never baked into the image.

```bash
az keyvault secret set \
  --vault-name kv-githubutility-prod \
  --name "GitHubCopilotToken" \
  --value "ghp_xxxxxxxxxxxxxxxxxxxx"
```

### 3. Build and Push Docker Image

The image includes Node.js 22 LTS and the `@github/copilot` CLI, so no extra tooling is needed on the host.

```bash
# Login to ACR
ACR_NAME=$(az acr list --resource-group rg-githubutility-prod --query [0].name -o tsv)
az acr login --name $ACR_NAME

# Build and push (multi-stage build — SDK + runtime layers)
az acr build \
  --registry $ACR_NAME \
  --image githubutility:latest \
  --file Dockerfile .
```

### 4. Update Container App (add GITHUB_TOKEN secret)

```bash
# Add Key Vault secret as a Container Apps secret
az containerapp secret set \
  --name ca-githubutility-prod \
  --resource-group rg-githubutility-prod \
  --secrets "github-copilot-token=keyvaultref:https://kv-githubutility-prod.vault.azure.net/secrets/GitHubCopilotToken,identityref:<managed-identity-id>"

# Update the container image and wire the secret to GITHUB_TOKEN
az containerapp update \
  --name ca-githubutility-prod \
  --resource-group rg-githubutility-prod \
  --image "$ACR_NAME.azurecr.io/githubutility:latest" \
  --set-env-vars "GITHUB_TOKEN=secretref:github-copilot-token"
```

## Running and Testing the Docker Image Locally

Use this section to build and smoke-test the image on your dev machine before pushing to ACR.

### 1. Build the image

```bash
docker build -t githubutility:local .
```

The build installs Node.js 22 LTS and `@github/copilot` inside the runtime layer, so the first build takes a few minutes. Subsequent builds are fast due to layer caching.

### 2. Run the container

Azure AD auth is optional in development — if `AzureAd:TenantId` is absent the app starts in unauthenticated mode. The only required value is `GITHUB_TOKEN`.

**Minimal (no auth, JSON storage):**
```bash
docker run --rm -p 8080:8080 \
  -e GITHUB_TOKEN="ghp_xxxxxxxxxxxxxxxxxxxx" \
  githubutility:local
```

**With Azure AD auth and SQLite storage:**
```bash
docker run --rm -p 8080:8080 \
  -e GITHUB_TOKEN="ghp_xxxxxxxxxxxxxxxxxxxx" \
  -e AzureAd__TenantId="<tenant-id>" \
  -e AzureAd__ClientId="<client-id>" \
  -e AzureAd__ClientSecret="<client-secret>" \
  -e AuditStore__Provider="Json" \
  githubutility:local
```

> On Windows use `$env:GITHUB_TOKEN` in PowerShell, or pass `-e GITHUB_TOKEN=%GITHUB_TOKEN%` in cmd.

### 3. Verify the container is healthy

```bash
curl http://localhost:8080/health
# Expected: {"status":"Healthy","utc":"..."}
```

### 4. Exercise the API

```bash
# Trigger a data ingestion run
curl -X POST http://localhost:8080/api/ingestion/run

# Ask the chat agent a question
curl -X POST http://localhost:8080/api/chat/query \
  -H "Content-Type: application/json" \
  -d '{"query": "Show me all open pull requests"}'

# Get open PR report
curl http://localhost:8080/api/reports/open-prs
```

### 5. Inspect Copilot CLI logs

The `@github/copilot` CLI subprocess writes its own logs. To see them alongside the app logs, run with `Copilot__LogLevel` overridden:

```bash
docker run --rm -p 8080:8080 \
  -e GITHUB_TOKEN="ghp_xxxxxxxxxxxxxxxxxxxx" \
  -e Copilot__LogLevel="debug" \
  githubutility:local
```

### 6. Open a shell inside the running container

Useful for confirming the CLI binary is present and reachable:

```bash
# In a second terminal, get the container ID
docker ps

# Shell into it
docker exec -it <container-id> bash

# Confirm the Copilot CLI is on PATH
which copilot
copilot --version

# Confirm Node.js is present
node --version
```

### Troubleshooting local runs

| Symptom | Likely cause | Fix |
|---------|-------------|-----|
| `copilot: command not found` at startup | npm global bin not on PATH | Run `docker exec` and check `echo $PATH`; rebuild if needed |
| `401 Unauthorized` from Copilot CLI | Invalid or missing `GITHUB_TOKEN` | Ensure PAT has **Copilot Requests** (read + write) scope |
| `ValidateOnStart` exception | Missing required config value | Check `Copilot:CliPath` is `"copilot"` and `GITHUB_TOKEN` is set |
| Port already in use | Another process on 8080 | Use `-p 9090:8080` and `curl http://localhost:9090/health` |

---

## Verify Deployment

```bash
# Get Container App URL
CONTAINER_APP_URL=$(az containerapp show \
  --name ca-githubutility-prod \
  --resource-group rg-githubutility-prod \
  --query properties.configuration.ingress.fqdn -o tsv)

# Test health endpoint
curl https://$CONTAINER_APP_URL/health
```

## Troubleshooting

### Authentication Not Working

1. Verify redirect URI matches Container App URL
2. Check Key Vault secrets are set correctly
3. Verify App Registration has correct permissions
4. Check Container App logs: `az containerapp logs show --name ca-githubutility-prod --resource-group rg-githubutility-prod`

### Database Connection Issues

1. Verify SQL Server firewall allows Azure services
2. Check connection string in Key Vault
3. Verify database exists and is accessible
4. Run migration script manually if needed

### Container App Not Starting

1. Check logs: `az containerapp logs show --name ca-githubutility-prod --resource-group rg-githubutility-prod --follow`
2. Verify image exists in ACR
3. Check environment variables are set correctly
4. Verify Key Vault access permissions

## Next Steps

- Configure GitHub Actions for CI/CD (see `.github/workflows/deploy-azure.yml`)
- Set up monitoring and alerts
- Configure custom domain (if needed)
- Set up staging environment

For detailed information, see `docs/deployment-guide.md`.
