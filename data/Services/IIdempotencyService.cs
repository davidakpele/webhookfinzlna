namespace WebhooksAPI.Data.Services;

public interface IIdempotencyService
{

    Task<bool> TryAcquireAsync(string externalRef, TimeSpan ttl);

    Task MarkCompletedAsync(string externalRef);

    Task ReleaseAsync(string externalRef);
}
