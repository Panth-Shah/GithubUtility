# Azure Deployment Guide - GitHub Utility

## Overview

This guide provides step-by-step instructions for deploying GitHub Utility to Azure with Microsoft Entra ID (Azure AD) authentication, integrated with GitHub SSO for seamless credential management.

## Architecture

```
Internet
    ↓
Azure Front Door / Application Gateway (Optional)
    ↓
Azure Container Apps / App Service
    ↓
Microsoft Entra ID (Azure AD) Authentication
    ↓
GitHub Utility Application
    ↓
Azure SQL Database / PostgreSQL
    ↓
Azure Key Vault (Secrets)
```

## Prerequisites

- Azure subscription with appropriate permissions
- GitHub organization account
- Azure CLI installed and configured
- .NET 8 SDK installed locally
- Docker Desktop (for container builds)
- GitHub repository access

## Step 1: Azure Infrastructure Setup

### 1.1 Create Resource Group

```bash
az group create \
  --name rg-githubutility-prod \
  --location eastus
```

### 1.2 Create Azure Key Vault

```bash
az keyvault create \
  --name kv-githubutility-prod \
  --resource-group rg-githubutility-prod \
  --location eastus \
  --sku standard
```

### 1.3 Create Azure SQL Database

```bash
# Create SQL Server
az sql server create \
  --name sql-githubutility-prod \
  --resource-group rg-githubutility-prod \
  --location eastus \
  --admin-user sqladmin \
  --admin-password <generate-strong-password>

# Create SQL Database
az sql db create \
  --resource-group rg-githubutility-prod \
  --server sql-githubutility-prod \
  --name githubutility-db \
  --service-objective S2 \
  --backup-storage-redundancy Local

# Store connection string in Key Vault
az keyvault secret set \
  --vault-name kv-githubutility-prod \
  --name "AuditStore--ConnectionString" \
  --value "Server=tcp:sql-githubutility-prod.database.windows.net,1433;Initial Catalog=githubutility-db;Persist Security Info=False;User ID=sqladmin;Password=<password>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
```

**Alternative: Azure Database for PostgreSQL**

```bash
# Create PostgreSQL Flexible Server
az postgres flexible-server create \
  --name psql-githubutility-prod \
  --resource-group rg-githubutility-prod \
  --location eastus \
  --admin-user pgadmin \
  --admin-password <generate-strong-password> \
  --sku-name Standard_B2s \
  --tier Burstable \
  --version 15 \
  --storage-size 32 \
  --public-access 0.0.0.0
```

## Step 2: Microsoft Entra ID (Azure AD) App Registration

### 2.1 Register Application

```bash
# Create App Registration
az ad app create \
  --display-name "GitHub Utility" \
  --web-redirect-uris "https://githubutility.azurecontainerapps.io/.auth/login/aad/callback" \
  --enable-id-token-issuance true

# Note the App ID (Client ID) - you'll need this
APP_ID=$(az ad app list --display-name "GitHub Utility" --query [0].appId -o tsv)
echo "App ID: $APP_ID"

# Create Service Principal
az ad sp create --id $APP_ID
```

### 2.2 Configure Authentication

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to **Microsoft Entra ID** → **App registrations** → **GitHub Utility**
3. Click **Authentication**
4. Add platform: **Web**
5. Add redirect URI: `https://<your-app-url>/.auth/login/aad/callback`
6. Enable **ID tokens** and **Access tokens**
7. Click **Save**

### 2.3 Configure API Permissions

1. Go to **API permissions**
2. Click **Add a permission**
3. Select **Microsoft Graph**
4. Add **Delegated permissions**:
   - `User.Read` (for user profile)
   - `email` (for email address)
5. Click **Add permissions**
6. Click **Grant admin consent** (if you have admin rights)

### 2.4 Configure GitHub SSO Integration

#### Option A: GitHub as Identity Provider (Recommended)

1. In Azure AD, go to **Enterprise applications**
2. Click **New application** → **Create your own application**
3. Name: "GitHub Utility"
4. Select **Integrate any other application you don't find in the gallery**
5. Click **Create**

6. Go to **Single sign-on** → **SAML**
7. Configure SAML settings:
   - **Identifier (Entity ID)**: `https://github.com/orgs/<your-org>`
   - **Reply URL**: `https://github.com/orgs/<your-org>/saml/consume`
   - **Sign on URL**: `https://github.com/orgs/<your-org>/sso/sign-in`

8. Download the **SAML metadata** and configure in GitHub:
   - GitHub → Organization Settings → Security → SAML single sign-on
   - Upload the metadata file

#### Option B: Use GitHub OAuth App (Alternative)

1. Create GitHub OAuth App:
   - GitHub → Settings → Developer settings → OAuth Apps
   - Create new OAuth App
   - Authorization callback URL: `https://<your-app-url>/.auth/login/github/callback`

2. Store credentials in Key Vault:
```bash
az keyvault secret set \
  --vault-name kv-githubutility-prod \
  --name "GitHub--ClientId" \
  --value "<github-oauth-client-id>"

az keyvault secret set \
  --vault-name kv-githubutility-prod \
  --name "GitHub--ClientSecret" \
  --value "<github-oauth-client-secret>"
```

### 2.5 Store App Registration Details in Key Vault

```bash
# Store Client ID
az keyvault secret set \
  --vault-name kv-githubutility-prod \
  --name "AzureAd--ClientId" \
  --value "$APP_ID"

# Generate and store Client Secret
CLIENT_SECRET=$(az ad app credential reset --id $APP_ID --query password -o tsv)
az keyvault secret set \
  --vault-name kv-githubutility-prod \
  --name "AzureAd--ClientSecret" \
  --value "$CLIENT_SECRET"

# Store Tenant ID
TENANT_ID=$(az account show --query tenantId -o tsv)
az keyvault secret set \
  --vault-name kv-githubutility-prod \
  --name "AzureAd--TenantId" \
  --value "$TENANT_ID"

# Store Instance URL
az keyvault secret set \
  --vault-name kv-githubutility-prod \
  --name "AzureAd--Instance" \
  --value "https://login.microsoftonline.com/"
```

## Step 3: Configure Application Authentication

### 3.1 Update Application Code

The application code has been updated to include Microsoft Entra ID authentication. See `src/GithubUtility.App/Program.cs` for authentication middleware configuration.

### 3.2 Configure App Settings

Update `appsettings.json` or use Azure App Configuration/Key Vault references:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "@Microsoft.KeyVault(SecretUri=https://kv-githubutility-prod.vault.azure.net/secrets/AzureAd--TenantId/)",
    "ClientId": "@Microsoft.KeyVault(SecretUri=https://kv-githubutility-prod.vault.azure.net/secrets/AzureAd--ClientId/)",
    "ClientSecret": "@Microsoft.KeyVault(SecretUri=https://kv-githubutility-prod.vault.azure.net/secrets/AzureAd--ClientSecret/)",
    "CallbackPath": "/.auth/login/aad/callback",
    "SignedOutCallbackPath": "/.auth/logout/aad/callback"
  },
  "AuditStore": {
    "Provider": "SqlServer",
    "ConnectionString": "@Microsoft.KeyVault(SecretUri=https://kv-githubutility-prod.vault.azure.net/secrets/AuditStore--ConnectionString/)",
    "InitializeSchemaOnStartup": true
  },
  "GitHubConnector": {
    "Mode": "Mcp",
    "Mcp": {
      "Endpoint": "@Microsoft.KeyVault(SecretUri=https://kv-githubutility-prod.vault.azure.net/secrets/GitHubConnector--Mcp--Endpoint/)",
      "ApiKey": "@Microsoft.KeyVault(SecretUri=https://kv-githubutility-prod.vault.azure.net/secrets/GitHubConnector--Mcp--ApiKey/)"
    }
  }
}
```

## Step 4: Containerization

### 4.1 Build Docker Image

See `Dockerfile` in the repository root. Build locally:

```bash
docker build -t githubutility:latest .
```

### 4.2 Test Locally

```bash
docker run -p 8080:8080 \
  -e AzureAd__TenantId="<tenant-id>" \
  -e AzureAd__ClientId="<client-id>" \
  -e AzureAd__ClientSecret="<client-secret>" \
  githubutility:latest
```

## Step 5: Deploy to Azure Container Apps

### 5.1 Create Container Apps Environment

```bash
az containerapp env create \
  --name cae-githubutility-prod \
  --resource-group rg-githubutility-prod \
  --location eastus
```

### 5.2 Create Azure Container Registry

```bash
az acr create \
  --name acrgithubutility \
  --resource-group rg-githubutility-prod \
  --sku Basic \
  --admin-enabled true
```

### 5.3 Build and Push Image

```bash
# Login to ACR
az acr login --name acrgithubutility

# Build and push
az acr build \
  --registry acrgithubutility \
  --image githubutility:latest \
  --file Dockerfile .
```

### 5.4 Create Container App

```bash
# Get ACR credentials
ACR_USERNAME=$(az acr credential show --name acrgithubutility --query username -o tsv)
ACR_PASSWORD=$(az acr credential show --name acrgithubutility --query passwords[0].value -o tsv)

# Create Container App
az containerapp create \
  --name ca-githubutility-prod \
  --resource-group rg-githubutility-prod \
  --environment cae-githubutility-prod \
  --image acrgithubutility.azurecr.io/githubutility:latest \
  --registry-server acrgithubutility.azurecr.io \
  --registry-username $ACR_USERNAME \
  --registry-password $ACR_PASSWORD \
  --target-port 8080 \
  --ingress external \
  --cpu 1.0 \
  --memory 2.0Gi \
  --min-replicas 1 \
  --max-replicas 3 \
  --env-vars \
    "AzureAd__Instance=https://login.microsoftonline.com/" \
    "AzureAd__TenantId=<tenant-id>" \
    "AzureAd__ClientId=<client-id>" \
    "AzureAd__ClientSecret=<client-secret>" \
    "AuditStore__Provider=SqlServer" \
    "AuditStore__ConnectionString=<connection-string>" \
    "GitHubConnector__Mode=Mcp"
```

### 5.5 Configure Authentication

```bash
# Enable Easy Auth (Managed Identity)
az containerapp auth microsoft update \
  --name ca-githubutility-prod \
  --resource-group rg-githubutility-prod \
  --client-id-setting-name "AzureAd__ClientId" \
  --client-secret-setting-name "AzureAd__ClientSecret" \
  --tenant-id-setting-name "AzureAd__TenantId"
```

## Step 6: Configure Key Vault Access

### 6.1 Enable Managed Identity

```bash
# Enable system-assigned managed identity
az containerapp identity assign \
  --name ca-githubutility-prod \
  --resource-group rg-githubutility-prod \
  --system-assigned

# Get principal ID
PRINCIPAL_ID=$(az containerapp identity show \
  --name ca-githubutility-prod \
  --resource-group rg-githubutility-prod \
  --query principalId -o tsv)
```

### 6.2 Grant Key Vault Access

```bash
# Grant access to Key Vault
az keyvault set-policy \
  --name kv-githubutility-prod \
  --object-id $PRINCIPAL_ID \
  --secret-permissions get list
```

## Step 7: Configure GitHub MCP Server

### 7.1 Store MCP Configuration

```bash
# Store MCP endpoint
az keyvault secret set \
  --vault-name kv-githubutility-prod \
  --name "GitHubConnector--Mcp--Endpoint" \
  --value "https://api.github.com/mcp"

# Store MCP API key (GitHub Personal Access Token)
az keyvault secret set \
  --vault-name kv-githubutility-prod \
  --name "GitHubConnector--Mcp--ApiKey" \
  --value "<github-pat-with-repo-read-permissions>"
```

## Step 8: Database Migration

### 8.1 Run Initial Schema

Connect to your database and run the appropriate migration script:

- For SQL Server: `db/migrations/V1__init_sqlserver.sql`
- For PostgreSQL: `db/migrations/V1__init_postgres.sql`

Or use the application's `InitializeSchemaOnStartup` feature (set to `true` in configuration).

## Step 9: Configure GitHub Actions (CI/CD)

See `.github/workflows/deploy-azure.yml` for automated deployment pipeline.

## Step 10: Post-Deployment Verification

### 10.1 Test Authentication

1. Navigate to your application URL
2. You should be redirected to Microsoft login
3. After authentication, you should see the application

### 10.2 Test Endpoints

```bash
# Get access token (after login)
TOKEN=$(az account get-access-token --resource api://<client-id> --query accessToken -o tsv)

# Test health endpoint
curl -H "Authorization: Bearer $TOKEN" \
  https://<your-app-url>/health

# Test ingestion
curl -X POST -H "Authorization: Bearer $TOKEN" \
  https://<your-app-url>/api/ingestion/run

# Test chat endpoint
curl -X POST -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"query": "Show me open PRs"}' \
  https://<your-app-url>/api/chat/query
```

## Step 11: Monitoring and Logging

### 11.1 Enable Application Insights

```bash
# Create Application Insights
az monitor app-insights component create \
  --app ai-githubutility-prod \
  --location eastus \
  --resource-group rg-githubutility-prod \
  --application-type web

# Get instrumentation key
INSTRUMENTATION_KEY=$(az monitor app-insights component show \
  --app ai-githubutility-prod \
  --resource-group rg-githubutility-prod \
  --query instrumentationKey -o tsv)

# Add to Container App
az containerapp update \
  --name ca-githubutility-prod \
  --resource-group rg-githubutility-prod \
  --set-env-vars "ApplicationInsights__InstrumentationKey=$INSTRUMENTATION_KEY"
```

### 11.2 Configure Alerts

Set up alerts in Azure Monitor for:
- High error rates
- Slow response times
- Failed authentication attempts
- Database connection failures

## Security Best Practices

1. **Use Managed Identities** where possible
2. **Store secrets in Key Vault** (never in code or config files)
3. **Enable HTTPS only** (enforced by Container Apps)
4. **Use Private Endpoints** for database connections (if required)
5. **Enable Azure AD Conditional Access** policies
6. **Regular security audits** of Key Vault access
7. **Enable diagnostic logging** for all resources
8. **Use Azure Policy** to enforce compliance

## Troubleshooting

### Authentication Issues

- Verify App Registration redirect URIs match your app URL
- Check Key Vault access permissions
- Verify tenant ID, client ID, and client secret are correct
- Check Azure AD logs in Azure Portal

### Database Connection Issues

- Verify connection string format
- Check firewall rules (allow Azure services)
- Verify database exists and is accessible
- Check Key Vault secret values

### MCP Server Issues

- Verify GitHub PAT has correct permissions
- Check MCP endpoint URL
- Verify network connectivity from Container App
- Check application logs for detailed errors

## Cost Optimization

- Use **Container Apps Consumption Plan** for variable workloads
- Use **Azure SQL Database Serverless** for development/testing
- Enable **auto-pause** for non-production databases
- Use **Azure Key Vault Standard** tier (sufficient for most use cases)
- Monitor and optimize Container App scaling settings

## Next Steps

1. Set up automated backups for database
2. Configure disaster recovery plan
3. Set up staging environment
4. Implement blue-green deployment strategy
5. Add performance testing and load testing
6. Set up monitoring dashboards
7. Document runbook for operations team

## Support

For issues or questions:
- Check application logs in Container Apps
- Review Azure Monitor metrics
- Check Key Vault access logs
- Review Azure AD sign-in logs
