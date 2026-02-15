#!/bin/bash

# Azure AD Setup Script for GitHub Utility
# This script helps set up Azure AD App Registration for authentication

set -e

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo -e "${GREEN}Azure AD App Registration Setup for GitHub Utility${NC}"
echo ""

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo "Azure CLI is not installed. Please install it first."
    exit 1
fi

# Check if logged in
if ! az account show &> /dev/null; then
    echo "Not logged in to Azure. Please run 'az login' first."
    exit 1
fi

# Get current subscription and tenant
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)
SUBSCRIPTION_NAME=$(az account show --query name -o tsv)

echo "Subscription: $SUBSCRIPTION_NAME ($SUBSCRIPTION_ID)"
echo "Tenant ID: $TENANT_ID"
echo ""

# Prompt for app name
read -p "Enter application display name [GitHub Utility]: " APP_NAME
APP_NAME=${APP_NAME:-GitHub Utility}

# Prompt for redirect URI (can be updated later)
read -p "Enter redirect URI (or press Enter to skip): " REDIRECT_URI

echo ""
echo -e "${GREEN}Creating App Registration...${NC}"

# Create App Registration
if [ -z "$REDIRECT_URI" ]; then
    APP_JSON=$(az ad app create --display-name "$APP_NAME" --output json)
else
    APP_JSON=$(az ad app create --display-name "$APP_NAME" --web-redirect-uris "$REDIRECT_URI" --output json)
fi

APP_ID=$(echo $APP_JSON | jq -r '.appId')
OBJECT_ID=$(echo $APP_JSON | jq -r '.id')

echo -e "${GREEN}✓ App Registration created!${NC}"
echo "  App ID (Client ID): $APP_ID"
echo "  Object ID: $OBJECT_ID"
echo ""

# Create Service Principal
echo -e "${GREEN}Creating Service Principal...${NC}"
az ad sp create --id $APP_ID --output none
echo -e "${GREEN}✓ Service Principal created!${NC}"
echo ""

# Create Client Secret
echo -e "${GREEN}Creating Client Secret...${NC}"
SECRET_JSON=$(az ad app credential reset --id $APP_ID --output json)
CLIENT_SECRET=$(echo $SECRET_JSON | jq -r '.password')
SECRET_ID=$(echo $SECRET_JSON | jq -r '.appId')

echo -e "${GREEN}✓ Client Secret created!${NC}"
echo "  Client Secret: $CLIENT_SECRET"
echo ""
echo -e "${YELLOW}⚠ IMPORTANT: Save this Client Secret now - it won't be shown again!${NC}"
echo ""

# Configure API Permissions
echo -e "${GREEN}Configuring API Permissions...${NC}"
echo "Adding Microsoft Graph permissions..."

# Add User.Read permission
az ad app permission add \
  --id $APP_ID \
  --api 00000003-0000-0000-c000-000000000000 \
  --api-permissions e1fe6dd8-ba31-4d61-89e7-88639da4683d=Scope \
  --output none

echo -e "${GREEN}✓ Permissions added!${NC}"
echo ""

# Summary
echo -e "${GREEN}=== Setup Complete ===${NC}"
echo ""
echo "App Registration Details:"
echo "  Display Name: $APP_NAME"
echo "  App ID (Client ID): $APP_ID"
echo "  Tenant ID: $TENANT_ID"
echo "  Client Secret: $CLIENT_SECRET"
echo ""
echo "Next Steps:"
echo "1. Go to Azure Portal → Microsoft Entra ID → App registrations → $APP_NAME"
echo "2. Go to Authentication → Add redirect URI: https://<your-app-url>/.auth/login/aad/callback"
echo "3. Go to API permissions → Grant admin consent (if you have permissions)"
echo "4. Store these values in Azure Key Vault or use in deployment script"
echo ""
echo "To store in Key Vault (if Key Vault exists):"
echo "  az keyvault secret set --vault-name <vault-name> --name 'AzureAd--ClientId' --value '$APP_ID'"
echo "  az keyvault secret set --vault-name <vault-name> --name 'AzureAd--ClientSecret' --value '$CLIENT_SECRET'"
echo "  az keyvault secret set --vault-name <vault-name> --name 'AzureAd--TenantId' --value '$TENANT_ID'"
echo ""
