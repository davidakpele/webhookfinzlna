using StackExchange.Redis;

namespace WebhooksAPI.Data.Services;

/// <summary>
/// Uses Redis SET NX (set-if-not-exists) as a fast, distributed idempotency gate.
/// The key is the provider's ExternalRef. TTL prevents the key set from growing unbounded.
/// </summary>
public class IdempotencyService(IConnectionMultiplexer redis) : IIdempotencyService
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<bool> TryAcquireAsync(string key, TimeSpan ttl)
    {
        // SET key "1" NX EX <ttl> — atomic, returns true only on first set
        return await _db.StringSetAsync(
            $"idempotency:{key}",
            "1",
            ttl,
            When.NotExists);
    }
}
