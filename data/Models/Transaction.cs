namespace WebhooksAPI.Data.Models;

public class Transaction
{
    public Guid Id { get; set; }

    public string ExternalRef { get; set; } = default!;

    public string AccountId { get; set; } = default!;
    public decimal Amount { get; set; }

    public string Currency { get; set; } = default!;

    public string Type { get; set; } = default!;

    public string Status { get; set; } = default!;

    public DateTime TransactedAt { get; set; }
    public DateTime ReceivedAt { get; set; }

    public string? Metadata { get; set; }
}
