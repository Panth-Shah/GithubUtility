#!/bin/bash

# Azure Deployment Script for GitHub Utility
# This script automates the deployment to Azure

set -e

# Configuration
RESOURCE_GROUP="rg-githubutility-prod"
LOCATION="eastus"
APP_NAME="githubutility"
ENVIRONMENT="prod"
ACR_NAME="acr${APP_NAME}${ENVIRONMENT}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}Starting Azure deployment for GitHub Utility...${NC}"

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo -e "${RED}Azure CLI is not installed. Please install it first.${NC}"
    exit 1
fi

# Check if logged in
if ! az account show &> /dev/null; then
    echo -e "${YELLOW}Not logged in to Azure. Please run 'az login' first.${NC}"
    exit 1
fi

# Get Azure AD details
echo -e "${GREEN}Getting Azure AD configuration...${NC}"
TENANT_ID=$(az account show --query tenantId -o tsv)
echo "Tenant ID: $TENANT_ID"

# Prompt for App Registration Client ID
read -p "Enter Azure AD App Registration Client ID: " CLIENT_ID

# Prompt for SQL Admin credentials
read -sp "Enter SQL Server admin username: " SQL_ADMIN_USERNAME
echo
read -sp "Enter SQL Server admin password: " SQL_ADMIN_PASSWORD
echo

# Prompt for GitHub MCP API Key
read -sp "Enter GitHub MCP API Key (Personal Access Token): " GITHUB_MCP_API_KEY
echo

# Create resource group if it doesn't exist
echo -e "${GREEN}Creating resource group...${NC}"
az group create \
  --name "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --output none

# Deploy infrastructure using Bicep
echo -e "${GREEN}Deploying infrastructure...${NC}"
az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file infrastructure/main.bicep \
  --parameters \
    appName="$APP_NAME" \
    environment="$ENVIRONMENT" \
    location="$LOCATION" \
    tenantId="$TENANT_ID" \
    clientId="$CLIENT_ID" \
    sqlAdminUsername="$SQL_ADMIN_USERNAME" \
    sqlAdminPassword="$SQL_ADMIN_PASSWORD" \
    githubMcpApiKey="$GITHUB_MCP_API_KEY" \
  --output json > deployment-output.json

echo -e "${GREEN}Infrastructure deployed successfully!${NC}"

# Get outputs
CONTAINER_APP_URL=$(jq -r '.properties.outputs.containerAppUrl.value' deployment-output.json)
KEY_VAULT_NAME=$(jq -r '.properties.outputs.keyVaultName.value' deployment-output.json)

echo -e "${GREEN}Container App URL: $CONTAINER_APP_URL${NC}"
echo -e "${GREEN}Key Vault Name: $KEY_VAULT_NAME${NC}"

# Build and push Docker image
echo -e "${GREEN}Building and pushing Docker image...${NC}"

# Login to ACR
az acr login --name "$ACR_NAME"

# Build and push
az acr build \
  --registry "$ACR_NAME" \
  --image "${APP_NAME}:latest" \
  --file Dockerfile . \
  --output none

echo -e "${GREEN}Docker image built and pushed successfully!${NC}"

# Update Container App to use new image
echo -e "${GREEN}Updating Container App...${NC}"
az containerapp update \
  --name "ca-${APP_NAME}-${ENVIRONMENT}" \
  --resource-group "$RESOURCE_GROUP" \
  --image "${ACR_NAME}.azurecr.io/${APP_NAME}:latest" \
  --output none

echo -e "${GREEN}Container App updated!${NC}"

# Wait for deployment to complete
echo -e "${YELLOW}Waiting for deployment to stabilize...${NC}"
sleep 30

# Test health endpoint
echo -e "${GREEN}Testing deployment...${NC}"
HEALTH_RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" "https://${CONTAINER_APP_URL#https://}/health" || echo "000")

if [ "$HEALTH_RESPONSE" = "200" ]; then
    echo -e "${GREEN}✓ Health check passed!${NC}"
else
    echo -e "${YELLOW}⚠ Health check returned: $HEALTH_RESPONSE${NC}"
    echo -e "${YELLOW}Please check the Container App logs for details.${NC}"
fi

echo -e "${GREEN}Deployment completed!${NC}"
echo -e "${GREEN}Application URL: $CONTAINER_APP_URL${NC}"
