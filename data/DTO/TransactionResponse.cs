namespace WebhooksAPI.Data.DTO;

public record TransactionResponse(
    Guid Id,
    string ExternalRef,
    string AccountId,
    decimal Amount,
    string Currency,
    string Type,
    string Status,
    DateTime TransactedAt,
    DateTime ReceivedAt,
    AccountSummaryDto AccountSummary
);

public record AccountSummaryDto(
    string AccountId,
    decimal TotalCredits,
    decimal TotalDebits,
    decimal RunningBalance,
    int TransactionCount,
    DateTime LastUpdated
);

/// <summary>
/// Discriminated result returned by the service layer.
/// The controller maps each case to the appropriate HTTP response.
/// </summary>
public abstract record ProcessResult
{
    /// <summary>New transaction accepted and stored.</summary>
    public record Accepted(TransactionResponse Transaction) : ProcessResult;

    /// <summary>Exact same ExternalRef already exists — true duplicate.</summary>
    public record DuplicateRequest(string ExternalRef) : ProcessResult;

    /// <summary>Account already has a pending transaction in flight.</summary>
    public record PendingTransactionExists(string AccountId, Guid PendingTransactionId) : ProcessResult;
}
