using StackExchange.Redis;

namespace WebhooksAPI.Data.Services;

public class IdempotencyService(IConnectionMultiplexer redis, ILogger<IdempotencyService> logger)
    : IIdempotencyService
{
  
    public static readonly TimeSpan PendingTtl = TimeSpan.FromMinutes(30);

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
            return true; 
        }
    }

    public async Task MarkCompletedAsync(string externalRef)
    {
        try
        {
            var db = redis.GetDatabase();
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
