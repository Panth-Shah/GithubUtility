# ── Stage 1: build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/GithubUtility.Core/GithubUtility.Core.csproj src/GithubUtility.Core/
COPY src/GithubUtility.App/GithubUtility.App.csproj   src/GithubUtility.App/
COPY appsettings.Production.json ./
RUN dotnet restore src/GithubUtility.App/GithubUtility.App.csproj

COPY src/ src/
RUN dotnet publish src/GithubUtility.App/GithubUtility.App.csproj \
    -c Release -o /app/publish --no-restore

# ── Stage 2: runtime ──────────────────────────────────────────────────────────
# Use the standard ASP.NET runtime image (Debian Bookworm-based).
# We add Node.js 22 LTS so the GitHub Copilot CLI (@github/copilot npm package)
# can be installed and invoked by the GitHub.Copilot.SDK NuGet package at runtime.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install Node.js 22 LTS (required by the @github/copilot npm package)
RUN apt-get update && apt-get install -y --no-install-recommends \
        curl ca-certificates gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && npm install -g pnpm \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

# Install the GitHub Copilot CLI.
# The GitHub.Copilot.SDK NuGet package spawns this binary as a child process.
# Authentication uses the GITHUB_TOKEN env var (needs "Copilot Requests" permission).
ARG COPILOT_VERSION=latest
RUN npm install -g @github/copilot@${COPILOT_VERSION}

# Verify the binary is on PATH
RUN which copilot && echo "GitHub Copilot CLI installed at $(which copilot)"

# ── Runtime environment ────────────────────────────────────────────────────────
# GITHUB_TOKEN must carry a PAT with "Copilot Requests" (read + write) permission.
# Supply it at deploy time via an Azure Container Apps secret or Key Vault reference.
# Never hard-code it here.
ENV GITHUB_TOKEN=""

# Tell the app where the Copilot CLI binary lives (matches npm global bin on this image).
# CopilotClientOptions.CliPath is driven by this env var through the options binding.
ENV Copilot__CliPath="copilot"

ENV ASPNETCORE_ENVIRONMENT="Production"
ENV ASPNETCORE_HTTP_PORTS="8080"

# XDG dirs for Copilot CLI state (sessions, compaction, etc.)
ENV XDG_CONFIG_HOME="/root/.config"
ENV XDG_STATE_HOME="/root/.local/state"
ENV XDG_DATA_HOME="/root/.local/share"

RUN mkdir -p /root/.config/copilot \
             /root/.local/state/copilot \
             /root/.local/share/copilot

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "GithubUtility.App.dll"]
