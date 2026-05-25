namespace WebhooksAPI.Data.Services;

public interface IIdempotencyService
{
    /// <summary>
    /// Attempts to acquire a lock for the given key.
    /// Returns true if the lock was acquired (first time seen within TTL).
    /// Returns false if the key already exists — duplicate request.
    /// </summary>
    Task<bool> TryAcquireAsync(string key, TimeSpan ttl);
}
