using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Claims;
using GithubUtility.App.Agents;
using GithubUtility.App.Connectors;
using GithubUtility.App.Endpoints;
using GithubUtility.App.Infrastructure;
using GithubUtility.App.Options;
using GithubUtility.App.Services;
using GithubUtility.App.Workers;
using GithubUtility.Core.Abstractions;
using GithubUtility.Core.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Serilog;
using Serilog.Events;

// Configure Serilog for structured logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithProperty("Application", "GithubUtility")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting GitHub Utility application");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog for logging
    builder.Host.UseSerilog();

    // Register ADO.NET providers for SQL database support
    System.Data.Common.DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", Microsoft.Data.SqlClient.SqlClientFactory.Instance);
    System.Data.Common.DbProviderFactories.RegisterFactory("Npgsql", Npgsql.NpgsqlFactory.Instance);
    System.Data.Common.DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", Microsoft.Data.Sqlite.SqliteFactory.Instance);

    // Enable options validation with data annotations
    builder.Services.AddOptions<SchedulerOptions>()
        .Bind(builder.Configuration.GetSection(SchedulerOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddOptions<AuditStoreOptions>()
        .Bind(builder.Configuration.GetSection(AuditStoreOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddOptions<GitHubConnectorOptions>()
        .Bind(builder.Configuration.GetSection(GitHubConnectorOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddOptions<AzureAdOptions>()
        .Bind(builder.Configuration.GetSection(AzureAdOptions.SectionName))
        .ValidateDataAnnotations()
        .ValidateOnStart();

    // Configure Microsoft Entra ID Authentication
    var azureAdOptions = builder.Configuration.GetSection(AzureAdOptions.SectionName).Get<AzureAdOptions>();
    if (azureAdOptions != null && !string.IsNullOrWhiteSpace(azureAdOptions.TenantId))
    {
        builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(options =>
            {
                builder.Configuration.Bind(AzureAdOptions.SectionName, options);
                options.Events = new OpenIdConnectEvents
                {
                    OnTokenValidated = context =>
                    {
                        // Add custom claims if needed
                        var claimsIdentity = context.Principal?.Identity as ClaimsIdentity;
                        if (claimsIdentity != null)
                        {
                            // Extract email from token
                            var email = context.Principal?.FindFirst(ClaimTypes.Email)?.Value
                                ?? context.Principal?.FindFirst("preferred_username")?.Value;
                            if (!string.IsNullOrWhiteSpace(email))
                            {
                                claimsIdentity.AddClaim(new Claim(ClaimTypes.Email, email));
                            }
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddAuthorization(options =>
        {
            // Require authentication for all API endpoints except health check
            options.FallbackPolicy = options.DefaultPolicy;
        });
    }
    else
    {
        // Development mode - allow unauthenticated access with warning
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();
    }

    // Configure rate limiting
    builder.Services.AddRateLimiter(options =>
    {
        // Fixed window rate limiter for API endpoints
        options.AddFixedWindowLimiter("api", limiterOptions =>
        {
            limiterOptions.PermitLimit = 100;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
            limiterOptions.QueueLimit = 10;
        });

        // More restrictive limiter for ingestion endpoint
        options.AddFixedWindowLimiter("ingestion", limiterOptions =>
        {
            limiterOptions.PermitLimit = 5;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
            limiterOptions.QueueLimit = 2;
        });

        // Very restrictive for chat endpoint (AI calls are expensive)
        options.AddFixedWindowLimiter("chat", limiterOptions =>
        {
            limiterOptions.PermitLimit = 20;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
            limiterOptions.QueueLimit = 5;
        });

        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsJsonAsync(new 
            { 
                Error = "Too many requests. Please try again later.",
                RetryAfter = context.Lease.TryGetMetadata(System.Threading.RateLimiting.MetadataName.RetryAfter, out var retryAfter) 
                    ? retryAfter.TotalSeconds 
                    : 60
            }, cancellationToken: token);
        };
    });

    // Add API documentation
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "GitHub Utility API",
            Version = "v1",
            Description = "API for auditing GitHub pull requests with AI-powered insights",
            Contact = new Microsoft.OpenApi.Models.OpenApiContact
            {
                Name = "GitHub Utility Team"
            }
        });

        // Add security definition for Azure AD
        if (azureAdOptions != null && !string.IsNullOrWhiteSpace(azureAdOptions.TenantId))
        {
            options.AddSecurityDefinition("oauth2", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.OAuth2,
                Flows = new Microsoft.OpenApi.Models.OpenApiOAuthFlows
                {
                    Implicit = new Microsoft.OpenApi.Models.OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri($"https://login.microsoftonline.com/{azureAdOptions.TenantId}/oauth2/v2.0/authorize"),
                        Scopes = new Dictionary<string, string>
                        {
                            { "User.Read", "Read user profile" },
                            { "email", "Read user email" }
                        }
                    }
                }
            });

            options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "oauth2"
                        }
                    },
                    new[] { "User.Read", "email" }
                }
            });
        }
    });

    // Configure telemetry
    var meter = new Meter("GithubUtility", "1.0.0");
    var ingestionCounter = meter.CreateCounter<int>("ingestion.runs", description: "Number of ingestion runs");
    var ingestionDuration = meter.CreateHistogram<double>("ingestion.duration", unit: "ms", description: "Duration of ingestion runs");
    var prProcessedCounter = meter.CreateCounter<int>("ingestion.prs_processed", description: "Number of PRs processed");
    var apiRequestCounter = meter.CreateCounter<int>("api.requests", description: "Number of API requests");
    var apiDuration = meter.CreateHistogram<double>("api.duration", unit: "ms", description: "API request duration");

    var activitySource = new ActivitySource("GithubUtility");

    // Add telemetry to DI
    builder.Services.AddSingleton(meter);
    builder.Services.AddSingleton(activitySource);

    builder.Services.AddHttpClient();
    builder.Services.AddHttpClient<IMcpToolClient, McpHttpToolClient>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(90);
    });

    // Add memory caching
    builder.Services.AddMemoryCache();

    builder.Services.AddSingleton<IGitHubDataSource>(serviceProvider =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<GitHubConnectorOptions>>().Value;
        return options.Mode.ToLowerInvariant() switch
        {
            "mcp" => ActivatorUtilities.CreateInstance<McpGitHubDataSource>(serviceProvider),
            "sample" => ActivatorUtilities.CreateInstance<SampleGitHubDataSource>(serviceProvider),
            _ => throw new InvalidOperationException(
                $"Unsupported GitHubConnector:Mode '{options.Mode}'. Supported values: Sample, Mcp.")
        };
    });

    builder.Services.AddSingleton<IAuditRepository>(serviceProvider =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<AuditStoreOptions>>().Value;
        return options.Provider.Equals("json", StringComparison.OrdinalIgnoreCase)
            ? ActivatorUtilities.CreateInstance<JsonAuditRepository>(serviceProvider)
            : ActivatorUtilities.CreateInstance<SqlAuditRepository>(serviceProvider);
    });

    builder.Services.AddSingleton<IPrAuditOrchestrator, PrAuditOrchestrator>();
    
    // Decorate orchestrator with caching
    builder.Services.AddSingleton<CachedPrAuditOrchestrator>(serviceProvider =>
    {
        var inner = serviceProvider.GetRequiredService<IPrAuditOrchestrator>();
        var cache = serviceProvider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
        var logger = serviceProvider.GetRequiredService<ILogger<CachedPrAuditOrchestrator>>();
        return new CachedPrAuditOrchestrator(inner, cache, logger);
    });
    
    // Register the cached version as the primary interface
    builder.Services.AddSingleton<IPrAuditOrchestrator>(serviceProvider =>
        serviceProvider.GetRequiredService<CachedPrAuditOrchestrator>());

    builder.Services.AddSingleton<IPrAuditChatAgent, PrAuditChatAgent>();

    builder.Services.AddHostedService<ScheduledIngestionWorker>();

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "GitHub Utility API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "GitHub Utility API";
        });
    }
    else
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    // Request logging and metrics
    app.Use(async (context, next) =>
    {
        var startTime = Stopwatch.GetTimestamp();
        var path = context.Request.Path.Value ?? "unknown";
        
        using var activity = activitySource.StartActivity("HTTP Request", ActivityKind.Server);
        activity?.SetTag("http.method", context.Request.Method);
        activity?.SetTag("http.path", path);
        activity?.SetTag("http.scheme", context.Request.Scheme);

        try
        {
            await next();
            
            var elapsedMs = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;
            apiRequestCounter.Add(1, new KeyValuePair<string, object?>("path", path), 
                                     new KeyValuePair<string, object?>("status", context.Response.StatusCode));
            apiDuration.Record(elapsedMs, new KeyValuePair<string, object?>("path", path));
            
            activity?.SetTag("http.status_code", context.Response.StatusCode);
            
            Log.Information("HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:0.00}ms",
                context.Request.Method, path, context.Response.StatusCode, elapsedMs);
        }
        catch (Exception ex)
        {
            var elapsedMs = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;
            activity?.SetTag("error", true);
            activity?.SetTag("exception.message", ex.Message);
            
            Log.Error(ex, "HTTP {Method} {Path} failed after {ElapsedMs:0.00}ms", 
                context.Request.Method, path, elapsedMs);
            throw;
        }
    });

    // Health check endpoint (no auth required)
    app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Utc = DateTimeOffset.UtcNow }))
        .AllowAnonymous();

    // Root endpoint (no auth required for basic status)
    app.MapGet("/", () => Results.Ok(new
    {
        Service = "GithubUtility",
        Status = "Running",
        Utc = DateTimeOffset.UtcNow
    }))
        .AllowAnonymous();

    // Map API endpoints using route groups
    var ingestionGroup = app.MapGroup("/api/ingestion");
    ingestionGroup.MapIngestionEndpoints();

    var reportsGroup = app.MapGroup("/api/reports");
    reportsGroup.MapReportsEndpoints();

    var chatGroup = app.MapGroup("/api/chat");
    chatGroup.MapChatEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
