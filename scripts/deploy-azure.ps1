# Azure Deployment Script for GitHub Utility (PowerShell)
# This script automates the deployment to Azure

param(
    [string]$ResourceGroup = "rg-githubutility-prod",
    [string]$Location = "eastus",
    [string]$AppName = "githubutility",
    [string]$Environment = "prod"
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-Success { Write-Host $args -ForegroundColor Green }
function Write-Error { Write-Host $args -ForegroundColor Red }
function Write-Warning { Write-Host $args -ForegroundColor Yellow }

Write-Success "Starting Azure deployment for GitHub Utility..."

# Check if Azure CLI is installed
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI is not installed. Please install it first."
    exit 1
}

# Check if logged in
try {
    $null = az account show 2>$null
} catch {
    Write-Warning "Not logged in to Azure. Please run 'az login' first."
    exit 1
}

# Get Azure AD details
Write-Success "Getting Azure AD configuration..."
$tenantId = az account show --query tenantId -o tsv
Write-Host "Tenant ID: $tenantId"

# Prompt for App Registration Client ID
$clientId = Read-Host "Enter Azure AD App Registration Client ID"

# Prompt for SQL Admin credentials
$sqlAdminUsername = Read-Host "Enter SQL Server admin username" -AsSecureString
$sqlAdminPassword = Read-Host "Enter SQL Server admin password" -AsSecureString

# Convert secure strings to plain text (for Bicep parameters)
$BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($sqlAdminUsername)
$sqlAdminUsernamePlain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
$BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($sqlAdminPassword)
$sqlAdminPasswordPlain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)

# Prompt for GitHub MCP API Key
$githubMcpApiKey = Read-Host "Enter GitHub MCP API Key (Personal Access Token)" -AsSecureString
$BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($githubMcpApiKey)
$githubMcpApiKeyPlain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)

$acrName = "acr${AppName}${Environment}"

# Create resource group if it doesn't exist
Write-Success "Creating resource group..."
az group create `
  --name $ResourceGroup `
  --location $Location `
  --output none

# Deploy infrastructure using Bicep
Write-Success "Deploying infrastructure..."
$deploymentOutput = az deployment group create `
  --resource-group $ResourceGroup `
  --template-file infrastructure/main.bicep `
  --parameters `
    appName=$AppName `
    environment=$Environment `
    location=$Location `
    tenantId=$tenantId `
    clientId=$clientId `
    sqlAdminUsername=$sqlAdminUsernamePlain `
    sqlAdminPassword=$sqlAdminPasswordPlain `
  --parameters githubMcpApiKey=$githubMcpApiKeyPlain `
  --output json | ConvertFrom-Json

Write-Success "Infrastructure deployed successfully!"

# Get outputs
$containerAppUrl = $deploymentOutput.properties.outputs.containerAppUrl.value
$keyVaultName = $deploymentOutput.properties.outputs.keyVaultName.value

Write-Success "Container App URL: $containerAppUrl"
Write-Success "Key Vault Name: $keyVaultName"

# Build and push Docker image
Write-Success "Building and pushing Docker image..."

# Login to ACR
az acr login --name $acrName

# Build and push
az acr build `
  --registry $acrName `
  --image "${AppName}:latest" `
  --file Dockerfile . `
  --output none

Write-Success "Docker image built and pushed successfully!"

# Update Container App to use new image
Write-Success "Updating Container App..."
az containerapp update `
  --name "ca-${AppName}-${Environment}" `
  --resource-group $ResourceGroup `
  --image "${acrName}.azurecr.io/${AppName}:latest" `
  --output none

Write-Success "Container App updated!"

# Wait for deployment to complete
Write-Warning "Waiting for deployment to stabilize..."
Start-Sleep -Seconds 30

# Test health endpoint
Write-Success "Testing deployment..."
try {
    $healthResponse = Invoke-WebRequest -Uri "$containerAppUrl/health" -UseBasicParsing -TimeoutSec 10
    if ($healthResponse.StatusCode -eq 200) {
        Write-Success "✓ Health check passed!"
    } else {
        Write-Warning "⚠ Health check returned: $($healthResponse.StatusCode)"
    }
} catch {
    Write-Warning "⚠ Health check failed. Please check the Container App logs for details."
}

Write-Success "Deployment completed!"
Write-Success "Application URL: $containerAppUrl"
