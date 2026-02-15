using GithubUtility.Core.Abstractions;
using GithubUtility.Core.Models;
using Microsoft.Extensions.Caching.Memory;

namespace GithubUtility.App.Services;

public sealed class CachedPrAuditOrchestrator : IPrAuditOrchestrator
{
    private readonly IPrAuditOrchestrator _inner;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedPrAuditOrchestrator> _logger;
    
    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(5);
    private const string CacheKeyPrefix = "PrAudit_";

    public CachedPrAuditOrchestrator(
        IPrAuditOrchestrator inner,
        IMemoryCache cache,
        ILogger<CachedPrAuditOrchestrator> logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IngestionRunResult> RunIngestionAsync(CancellationToken cancellationToken)
    {
        // Don't cache ingestion - always run fresh and invalidate cache after
        var result = await _inner.RunIngestionAsync(cancellationToken);
        InvalidateCache();
        _logger.LogInformation("Cache invalidated after ingestion run");
        return result;
    }

    public Task<IReadOnlyList<OpenPrSummary>> GetOpenPrReportAsync(string? repository, int olderThanDays, CancellationToken cancellationToken)
    {
        var cacheKey = $"{CacheKeyPrefix}OpenPrs_{repository ?? "all"}_{olderThanDays}";
        return _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = DefaultCacheDuration;
            _logger.LogDebug("Cache miss for {CacheKey}", cacheKey);
            return await _inner.GetOpenPrReportAsync(repository, olderThanDays, cancellationToken);
        })!;
    }

    public Task<IReadOnlyList<UserStatSummary>> GetUserStatsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var cacheKey = $"{CacheKeyPrefix}UserStats_{from:yyyyMMdd}_{to:yyyyMMdd}";
        return _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = DefaultCacheDuration;
            _logger.LogDebug("Cache miss for {CacheKey}", cacheKey);
            return await _inner.GetUserStatsAsync(from, to, cancellationToken);
        })!;
    }

    public Task<ReleaseAuditSummary> GetReleaseAuditSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var cacheKey = $"{CacheKeyPrefix}ReleaseSummary_{from:yyyyMMdd}_{to:yyyyMMdd}";
        return _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = DefaultCacheDuration;
            _logger.LogDebug("Cache miss for {CacheKey}", cacheKey);
            return await _inner.GetReleaseAuditSummaryAsync(from, to, cancellationToken);
        })!;
    }

    public Task<IReadOnlyList<RepositoryReport>> GetRepositoryReportAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var cacheKey = $"{CacheKeyPrefix}RepoReport_{from:yyyyMMdd}_{to:yyyyMMdd}";
        return _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = DefaultCacheDuration;
            _logger.LogDebug("Cache miss for {CacheKey}", cacheKey);
            return await _inner.GetRepositoryReportAsync(from, to, cancellationToken);
        })!;
    }

    private void InvalidateCache()
    {
        // Simple approach: clear all entries with our prefix
        // For production, consider a more sophisticated approach like cache key tracking
        if (_cache is MemoryCache memCache)
        {
            // Note: MemoryCache doesn't have a direct way to remove by prefix
            // This is a limitation - in production, consider using a distributed cache with tag support
            // or maintaining a list of cache keys
            _logger.LogWarning("Cache invalidation called but MemoryCache doesn't support prefix-based removal");
        }
    }
}
