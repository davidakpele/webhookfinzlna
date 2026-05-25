namespace WebhooksAPI.Data.Services;

public interface IIdempotencyService
{
    /// <summary>
    /// Attempts to acquire a lock for the given key with the specified TTL.
    /// Returns true if acquired (first time seen), false if the key already exists.
    /// </summary>
    Task<bool> TryAcquireAsync(string externalRef, TimeSpan ttl);

    /// <summary>
    /// Sets the key with a very short TTL (2 seconds) to signal completion.
    /// The key expires almost immediately, freeing the slot, but any retry
    /// within that window is still blocked. After expiry the DB unique constraint
    /// on ExternalRef permanently prevents re-use.
    /// </summary>
    Task MarkCompletedAsync(string externalRef);

    /// <summary>
    /// Deletes the key immediately — used when a pending transaction is rejected
    /// so the slot is not held unnecessarily.
    /// </summary>
    Task ReleaseAsync(string externalRef);
}
