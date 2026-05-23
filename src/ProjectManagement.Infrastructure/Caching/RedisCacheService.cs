using System.Text.Json;
using Microsoft.Extensions.Logging;
using ProjectManagement.Application.Interfaces;
using StackExchange.Redis;

namespace ProjectManagement.Infrastructure.Caching;

/// <summary>
/// Redis-backed cache service with graceful degradation.
/// When Redis is unavailable, cache operations degrade safely:
///   • GetAsync  → returns null (treated as cache miss; falls through to DB)
///   • SetAsync  → no-op (entry simply isn't cached this request)
///   • RemoveAsync → no-op (stale entries expire naturally via TTL)
/// The application remains fully functional without Redis running locally.
/// </summary>
public class RedisCacheService(
    IConnectionMultiplexer redis,
    ILogger<RedisCacheService> logger) : ICacheService
{
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);

    // GetDatabase() is cheap and does not open a connection — safe to call per-operation.
    private IDatabase Db => redis.GetDatabase();

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var json = await Db.StringGetAsync(key);
            if (json.IsNullOrEmpty) return default;
            return JsonSerializer.Deserialize<T>(json!);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex,
                "Redis unavailable — cache GET miss for key '{Key}'. Falling through to source.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(value);
            await Db.StringSetAsync(key, json, expiration ?? DefaultExpiration);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex,
                "Redis unavailable — cache SET skipped for key '{Key}'.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await Db.KeyDeleteAsync(key);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex,
                "Redis unavailable — cache REMOVE skipped for key '{Key}'. Entry will expire via TTL.", key);
        }
    }
}
