s# GitHub SSO Integration with Azure AD

## Overview

This guide explains how to configure GitHub SSO (Single Sign-On) with Azure AD so users can authenticate to GitHub Utility using their GitHub organization credentials.

## Architecture

```
User → GitHub Utility → Azure AD → GitHub SSO → Authenticated
```

When a user logs in:
1. They're redirected to Azure AD
2. Azure AD checks if they're part of the GitHub organization
3. If configured with GitHub SSO, Azure AD redirects to GitHub for authentication
4. After GitHub authentication, user is redirected back to the application

## Option 1: GitHub as Identity Provider (Recommended)

This approach uses GitHub as the primary identity provider, with Azure AD as the authentication broker.

### Step 1: Configure GitHub Organization SAML SSO

1. Go to your GitHub organization settings
2. Navigate to **Security** → **SAML single sign-on**
3. Click **Enable SAML authentication**
4. You'll need the SAML metadata from Azure AD (see Step 2)

### Step 2: Configure Azure AD Enterprise Application

1. In Azure Portal, go to **Microsoft Entra ID** → **Enterprise applications**
2. Click **New application** → **Create your own application**
3. Name: "GitHub Utility"
4. Select **Integrate any other application you don't find in the gallery**
5. Click **Create**

6. Go to **Single sign-on** → **SAML**
7. Click **Edit** to configure SAML settings

**Basic SAML Configuration:**
- **Identifier (Entity ID)**: `https://github.com/orgs/<your-org>`
- **Reply URL (Assertion Consumer Service URL)**: `https://github.com/orgs/<your-org>/saml/consume`
- **Sign on URL**: `https://github.com/orgs/<your-org>/sso/sign-in`

**Attributes & Claims:**
- Add claim: `name` → `user.name`
- Add claim: `email` → `user.email`
- Add claim: `login` → `user.login`

8. Download the **SAML Signing Certificate** (Base64)
9. Download the **Federation Metadata XML**

### Step 3: Configure GitHub with Azure AD SAML

1. In GitHub organization settings → **SAML single sign-on**
2. Click **Test SAML configuration**
3. Upload the **Federation Metadata XML** from Azure AD
   - Or manually enter:
     - **Sign on URL**: From Azure AD SAML configuration
     - **Issuer**: From Azure AD (usually the App ID URI)
     - **Public Certificate**: From Azure AD SAML certificate

4. Click **Test SAML configuration** to verify
5. Once verified, click **Enable SAML authentication**

### Step 4: Link Users

1. In Azure AD Enterprise Application, go to **Users and groups**
2. Assign users or groups who should have access
3. Users will need to authorize the application in GitHub

## Option 2: Azure AD with GitHub OAuth (Alternative)

This approach uses Azure AD as primary, but allows GitHub OAuth for users who prefer it.

### Step 1: Create GitHub OAuth App

1. Go to GitHub → **Settings** → **Developer settings** → **OAuth Apps**
2. Click **New OAuth App**
3. Fill in:
   - **Application name**: GitHub Utility
   - **Homepage URL**: `https://<your-app-url>`
   - **Authorization callback URL**: `https://<your-app-url>/.auth/login/github/callback`
4. Click **Register application**
5. **Note the Client ID and generate a Client Secret**

### Step 2: Store GitHub OAuth Credentials

```bash
az keyvault secret set \
  --vault-name kv-githubutility-prod \
  --name "GitHub--OAuth--ClientId" \
  --value "<github-oauth-client-id>"

az keyvault secret set \
  --vault-name kv-githubutility-prod \
  --name "GitHub--OAuth--ClientSecret" \
  --value "<github-oauth-client-secret>"
```

### Step 3: Configure Azure AD to Accept GitHub

1. In Azure AD App Registration, go to **Authentication**
2. Add **GitHub** as an identity provider (if supported)
3. Or configure as a custom OAuth provider

**Note:** This option requires additional configuration in the application code to support multiple identity providers.

## Option 3: Use Same Azure AD Tenant as GitHub Enterprise (If Applicable)

If your organization uses GitHub Enterprise Cloud with Azure AD integration:

1. GitHub Enterprise is already configured with Azure AD
2. Users authenticate to GitHub using Azure AD
3. Your application can use the same Azure AD tenant
4. Users will have seamless SSO experience

**Configuration:**
- Use the same Azure AD tenant that GitHub Enterprise uses
- Configure App Registration in that tenant
- Users will authenticate once and have access to both GitHub and your application

## Recommended Approach

For most use cases, **Option 1 (GitHub as Identity Provider)** is recommended because:

1. ✅ Users authenticate with their GitHub credentials
2. ✅ Leverages existing GitHub organization membership
3. ✅ Centralized user management in GitHub
4. ✅ Familiar authentication flow for developers
5. ✅ Works with GitHub's built-in SSO features

## Testing SSO

### Test Authentication Flow

1. Navigate to your application URL
2. You should be redirected to Azure AD login
3. If GitHub SSO is configured, you may be redirected to GitHub
4. After authentication, you should be redirected back to the application
5. Check that your user claims (email, name) are available

### Verify User Claims

Add a test endpoint to verify claims:

```csharp
app.MapGet("/api/user/me", (ClaimsPrincipal user) =>
{
    return Results.Ok(new
    {
        IsAuthenticated = user.Identity?.IsAuthenticated ?? false,
        Name = user.Identity?.Name,
        Email = user.FindFirst(ClaimTypes.Email)?.Value,
        Claims = user.Claims.Select(c => new { c.Type, c.Value })
    });
})
.RequireAuthorization();
```

## Troubleshooting

### Users Can't Authenticate

1. **Check Azure AD Sign-in Logs**
   - Azure Portal → Microsoft Entra ID → Sign-in logs
   - Look for failed authentication attempts
   - Check error messages

2. **Verify Redirect URIs**
   - App Registration → Authentication
   - Ensure redirect URI matches Container App URL exactly
   - Check for typos or missing protocols (http vs https)

3. **Check GitHub SSO Status**
   - GitHub → Organization Settings → Security → SAML single sign-on
   - Verify SAML is enabled and configured correctly
   - Check if users have authorized the application

### Users Authenticated but No Claims

1. **Check Token Configuration**
   - App Registration → Token configuration
   - Ensure required claims are included (email, name, etc.)

2. **Verify API Permissions**
   - App Registration → API permissions
   - Ensure Microsoft Graph permissions are granted
   - Grant admin consent if needed

### GitHub SSO Not Working

1. **Verify SAML Configuration**
   - Check SAML metadata is correctly uploaded to GitHub
   - Verify certificate is valid and not expired
   - Check entity IDs match between Azure AD and GitHub

2. **Test SAML Flow**
   - Use GitHub's "Test SAML configuration" feature
   - Check Azure AD SAML logs for errors

## Security Considerations

1. **Enable MFA** in Azure AD for additional security
2. **Use Conditional Access** policies to restrict access
3. **Regularly rotate** client secrets and certificates
4. **Monitor** authentication logs for suspicious activity
5. **Limit access** to specific users or groups in Azure AD
6. **Use Private Endpoints** for internal-only access (if needed)

## Next Steps

After SSO is configured:

1. Test authentication with multiple users
2. Configure role-based access control (if needed)
3. Set up monitoring and alerts for authentication failures
4. Document the authentication flow for users
5. Create runbook for troubleshooting authentication issues
