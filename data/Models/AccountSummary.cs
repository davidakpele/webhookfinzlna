namespace WebhooksAPI.Data.Models;


public class AccountSummary
{
    public string AccountId { get; set; } = default!;
    public decimal TotalCredits { get; set; }
    public decimal TotalDebits { get; set; }
    public int TransactionCount { get; set; }
    public DateTime LastUpdated { get; set; }
    public decimal RunningBalance => TotalCredits - TotalDebits;
}
