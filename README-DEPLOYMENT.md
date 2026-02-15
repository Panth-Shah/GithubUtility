# Quick Start Deployment Guide

## Prerequisites

1. **Azure CLI** installed and configured
2. **Azure Subscription** with appropriate permissions
3. **GitHub Personal Access Token** with `repo` read permissions
4. **Azure AD App Registration** created (see detailed guide)

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
    githubMcpApiKey=<github-pat>
```

### 2. Build and Push Docker Image

```bash
# Login to ACR
ACR_NAME=$(az acr list --resource-group rg-githubutility-prod --query [0].name -o tsv)
az acr login --name $ACR_NAME

# Build and push
az acr build \
  --registry $ACR_NAME \
  --image githubutility:latest \
  --file Dockerfile .
```

### 3. Update Container App

```bash
az containerapp update \
  --name ca-githubutility-prod \
  --resource-group rg-githubutility-prod \
  --image "$ACR_NAME.azurecr.io/githubutility:latest"
```

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
