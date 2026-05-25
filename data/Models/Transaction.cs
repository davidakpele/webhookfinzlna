namespace WebhooksAPI.Data.Models;

public class Transaction
{
    public Guid Id { get; set; }

    /// <summary>Unique reference from the external provider — used for idempotency.</summary>
    public string ExternalRef { get; set; } = default!;

    public string AccountId { get; set; } = default!;
    public decimal Amount { get; set; }

    /// <summary>ISO 4217 currency code, e.g. "USD".</summary>
    public string Currency { get; set; } = default!;

    /// <summary>"credit" or "debit".</summary>
    public string Type { get; set; } = default!;

    /// <summary>"pending", "completed", or "failed".</summary>
    public string Status { get; set; } = default!;

    public DateTime TransactedAt { get; set; }
    public DateTime ReceivedAt { get; set; }

    /// <summary>Raw JSON blob from the provider — stored as-is.</summary>
    public string? Metadata { get; set; }
}
