@description('The name of the resource group')
param resourceGroupName string = 'rg-githubutility-prod'

@description('The location for all resources')
param location string = resourceGroup().location

@description('The name of the application')
param appName string = 'githubutility'

@description('The environment (dev, staging, prod)')
param environment string = 'prod'

@description('Azure AD Tenant ID')
param tenantId string

@description('Azure AD Client ID (App Registration)')
param clientId string

@description('Azure AD Client Secret (App Registration Secret)')
@secure()
param clientSecret string

@description('SQL Server administrator username')
@secure()
param sqlAdminUsername string

@description('SQL Server administrator password')
@secure()
param sqlAdminPassword string

@description('GitHub MCP API Key (PAT with repo read permission — used by the ingestion connector)')
@secure()
param githubMcpApiKey string

@description('GitHub Personal Access Token with Copilot Requests (read+write) permission — used by the @github/copilot CLI inside the container')
@secure()
param githubCopilotToken string

@description('Model passed to each GitHub Copilot CLI session (e.g. gpt-4o, claude-sonnet-4-5)')
param copilotModel string = 'gpt-4o'

var keyVaultName = 'kv-${appName}-${environment}'
var sqlServerName = 'sql-${appName}-${environment}'
var sqlDatabaseName = '${appName}-db'
var containerAppEnvName = 'cae-${appName}-${environment}'
var containerAppName = 'ca-${appName}-${environment}'
var acrName = 'acr${appName}${environment}'
var appInsightsName = 'ai-${appName}-${environment}'

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    accessPolicies: []
    enabledForDeployment: true
    enabledForTemplateDeployment: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enableRbacAuthorization: false
  }
}

// Key Vault Secrets
resource azureAdTenantIdSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureAd--TenantId'
  properties: {
    value: tenantId
  }
}

resource azureAdClientIdSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureAd--ClientId'
  properties: {
    value: clientId
  }
}

resource azureAdClientSecretSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AzureAd--ClientSecret'
  properties: {
    value: clientSecret
  }
}

// SQL Server
resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminUsername
    administratorLoginPassword: sqlAdminPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// SQL Database
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: 'S2'
    tier: 'Standard'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 268435456000 // 250 GB
    requestedBackupStorageRedundancy: 'Local'
  }
}

// SQL Server Firewall Rule - Allow Azure Services
resource sqlFirewallRule 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Connection String Secret
resource sqlConnectionStringSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'AuditStore--ConnectionString'
  properties: {
    value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabaseName};Persist Security Info=False;User ID=${sqlAdminUsername};Password=${sqlAdminPassword};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
  }
}

// GitHub MCP API Key Secret (ingestion connector)
resource githubMcpApiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'GitHubConnector--Mcp--ApiKey'
  properties: {
    value: githubMcpApiKey
  }
}

// GitHub Copilot Token Secret (@github/copilot CLI authentication)
resource githubCopilotTokenSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'GitHubCopilotToken'
  properties: {
    value: githubCopilotToken
  }
}

// Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Request_Source: 'rest'
  }
}

// Container Registry
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: true
  }
}

// Container Apps Environment
resource containerAppEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: containerAppEnvName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: appInsights.properties.AppId
        sharedKey: appInsights.properties.InstrumentationKey
      }
    }
  }
}

// Container App
resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: containerAppName
  location: location
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: '${containerRegistry.name}.azurecr.io'
          identity: 'system'
        }
      ]
      secrets: [
        {
          name: 'azuread-tenant-id'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/AzureAd--TenantId'
          identity: 'system'
        }
        {
          name: 'azuread-client-id'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/AzureAd--ClientId'
          identity: 'system'
        }
        {
          name: 'azuread-client-secret'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/AzureAd--ClientSecret'
          identity: 'system'
        }
        {
          name: 'sql-connection-string'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/AuditStore--ConnectionString'
          identity: 'system'
        }
        {
          name: 'github-mcp-api-key'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/GitHubConnector--Mcp--ApiKey'
          identity: 'system'
        }
        {
          name: 'github-copilot-token'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/GitHubCopilotToken'
          identity: 'system'
        }
      ]
    }
    template: {
      containers: [
        {
          name: containerAppName
          image: '${containerRegistry.name}.azurecr.io/${appName}:latest'
          env: [
            {
              name: 'AzureAd__Instance'
              value: 'https://login.microsoftonline.com/'
            }
            {
              name: 'AzureAd__TenantId'
              secretRef: 'azuread-tenant-id'
            }
            {
              name: 'AzureAd__ClientId'
              secretRef: 'azuread-client-id'
            }
            {
              name: 'AzureAd__ClientSecret'
              secretRef: 'azuread-client-secret'
            }
            {
              name: 'AuditStore__Provider'
              value: 'SqlServer'
            }
            {
              name: 'AuditStore__ConnectionString'
              secretRef: 'sql-connection-string'
            }
            {
              name: 'AuditStore__InitializeSchemaOnStartup'
              value: 'true'
            }
            {
              name: 'GitHubConnector__Mode'
              value: 'Mcp'
            }
            {
              name: 'GitHubConnector__Mcp__ApiKey'
              secretRef: 'github-mcp-api-key'
            }
            // GitHub Copilot CLI authentication — forwarded into the CLI subprocess
            // by CopilotClientOptions.Environment at runtime
            {
              name: 'GITHUB_TOKEN'
              secretRef: 'github-copilot-token'
            }
            // Copilot CLI settings — match CopilotOptions section names
            {
              name: 'Copilot__CliPath'
              value: 'copilot'
            }
            {
              name: 'Copilot__Model'
              value: copilotModel
            }
            {
              name: 'ApplicationInsights__InstrumentationKey'
              value: appInsights.properties.InstrumentationKey
            }
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
          ]
          resources: {
            // 2 vCPU / 4 Gi — the .NET app + Node.js @github/copilot CLI subprocess
            // run concurrently; 1 vCPU / 2 Gi was too constrained under load
            cpu: json('2.0')
            memory: '4Gi'
          }
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
  identity: {
    type: 'SystemAssigned'
  }
}

// Grant Container App access to Key Vault
resource keyVaultAccessPolicy 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, containerApp.id, 'KeyVaultSecretsUser')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: containerApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Outputs
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output containerAppName string = containerApp.name
output containerAppUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output sqlServerName string = sqlServer.name
output sqlDatabaseName string = sqlDatabase.name
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey
