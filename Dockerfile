# Use the official .NET 8 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["src/GithubUtility.App/GithubUtility.App.csproj", "src/GithubUtility.App/"]
COPY ["src/GithubUtility.Core/GithubUtility.Core.csproj", "src/GithubUtility.Core/"]

# Restore dependencies
RUN dotnet restore "src/GithubUtility.App/GithubUtility.App.csproj"

# Copy all source files
COPY . .

# Build the application
WORKDIR "/src/src/GithubUtility.App"
RUN dotnet build "GithubUtility.App.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "GithubUtility.App.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Use the official .NET 8 runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create directory for data
RUN mkdir -p /app/data

# Copy published application
COPY --from=publish /app/publish .

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the application
ENTRYPOINT ["dotnet", "GithubUtility.App.dll"]
