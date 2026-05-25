using StackExchange.Redis;

namespace WebhooksAPI.Data.Services;

/// <summary>
/// Uses Redis SET NX as a fast, distributed idempotency gate.
/// If Redis is unavailable, the call is treated as a cache miss and the
/// DB-level ON CONFLICT DO NOTHING acts as the safety net — no request is lost.
/// </summary>
public class IdempotencyService(IConnectionMultiplexer redis, ILogger<IdempotencyService> logger)
    : IIdempotencyService
{
    public async Task<bool> TryAcquireAsync(string key, TimeSpan ttl)
    {
        try
        {
            var db = redis.GetDatabase();
            // SET key "1" NX EX <ttl> — atomic, returns true only on first set
            return await db.StringSetAsync($"idempotency:{key}", "1", ttl, When.NotExists);
        }
        catch (RedisException ex)
        {
            // Redis is down — log and degrade gracefully.
            // The DB upsert (ON CONFLICT DO NOTHING) will still prevent duplicates.
            logger.LogWarning(ex, "Redis unavailable for idempotency check on key '{Key}'. Falling through to DB.", key);
            return true; // treat as "not seen" — DB is the source of truth
        }
    }
}
