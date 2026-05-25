namespace WebhooksAPI.Data.Models;

/// <summary>
/// Derived record: recomputed after every completed transaction for an account.
/// RunningBalance is a computed column — not stored.
/// </summary>
public class AccountSummary
{
    public string AccountId { get; set; } = default!;
    public decimal TotalCredits { get; set; }
    public decimal TotalDebits { get; set; }
    public int TransactionCount { get; set; }
    public DateTime LastUpdated { get; set; }

    // Derived in-memory; ignored by EF Core
    public decimal RunningBalance => TotalCredits - TotalDebits;
}
