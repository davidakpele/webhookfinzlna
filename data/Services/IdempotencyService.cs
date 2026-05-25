using StackExchange.Redis;

namespace WebhooksAPI.Data.Services;

/// <summary>
/// Redis-backed idempotency gate with status-aware TTLs:
///   - pending   → 30 minutes  (covers realistic processing windows)
///   - completed → 2 seconds   (blocks immediate retries; DB unique constraint handles the rest)
///
/// If Redis is unavailable the service degrades gracefully — the PostgreSQL
/// ON CONFLICT DO NOTHING upsert acts as the permanent safety net.
/// </summary>
public class IdempotencyService(IConnectionMultiplexer redis, ILogger<IdempotencyService> logger)
    : IIdempotencyService
{
    // Pending transactions are held for 30 minutes max
    public static readonly TimeSpan PendingTtl = TimeSpan.FromMinutes(30);

    // Completed keys linger for 2 seconds to absorb immediate retries,
    // then expire — the DB unique constraint takes over permanently
    private static readonly TimeSpan CompletedTtl = TimeSpan.FromSeconds(2);

    private static string Key(string externalRef) => $"idempotency:{externalRef}";

    public async Task<bool> TryAcquireAsync(string externalRef, TimeSpan ttl)
    {
        try
        {
            var db = redis.GetDatabase();
            return await db.StringSetAsync(Key(externalRef), "1", ttl, When.NotExists);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex,
                "Redis unavailable for idempotency check on '{Key}'. Falling through to DB.", externalRef);
            return true; // treat as unseen — DB is the source of truth
        }
    }

    public async Task MarkCompletedAsync(string externalRef)
    {
        try
        {
            var db = redis.GetDatabase();
            // Overwrite with a 2-second TTL — blocks instant retries, then self-destructs
            await db.StringSetAsync(Key(externalRef), "completed", CompletedTtl);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex,
                "Redis unavailable when marking '{Key}' as completed.", externalRef);
        }
    }

    public async Task ReleaseAsync(string externalRef)
    {
        try
        {
            var db = redis.GetDatabase();
            await db.KeyDeleteAsync(Key(externalRef));
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex,
                "Redis unavailable when releasing idempotency key '{Key}'.", externalRef);
        }
    }
}
